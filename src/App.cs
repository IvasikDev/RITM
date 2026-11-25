using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("Ритм")]
[assembly: AssemblyDescription("Тренажёр слепой печати")]
[assembly: AssemblyProduct("Ритм")]
[assembly: AssemblyVersion(RhythmTyping.App.Version + ".0")]
[assembly: AssemblyInformationalVersion(RhythmTyping.App.Version)]

namespace RhythmTyping
{
    internal static class App
    {
        internal const string Version = "0.2.13";

        [STAThread]
        private static void Main()
        {
            Application application = new Application();
            using (Stream layout = Assembly.GetExecutingAssembly().GetManifestResourceStream("RhythmTyping.MainWindow.xaml"))
            {
                Window window = (Window)XamlReader.Load(layout);
                window.Height = Math.Min(window.Height, SystemParameters.WorkArea.Height);
                window.Width = Math.Min(window.Width, SystemParameters.WorkArea.Width);
                string progressDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RhythmTyping");
                new TrainerWindow(window, new ProgressStore(Path.Combine(progressDirectory, "course-progress.xml"),
                    Path.Combine(progressDirectory, "progress.xml")), new GuidePreferences(Path.Combine(progressDirectory, "guides.xml")));
                application.Run(window);
            }
        }
    }

    internal sealed class TrainerWindow
    {
        private readonly Brush Text;
        private readonly Brush Muted;
        private readonly Brush Pending;
        private readonly Brush Accent;
        private readonly Brush AccentInk;
        private readonly Brush KeyFill;
        private readonly Brush KeyOutline;
        private readonly Brush TabFill;
        private readonly Brush TabOutline;
        private readonly Brush SelectedLesson;
        private static readonly Brush Pink = HandGuide.ErrorInk;
        private static readonly Brush ErrorFill = HandGuide.ErrorFill;
        private static readonly Brush Transparent = Brushes.Transparent;
        private static readonly ScaleTransform CompactContent = new ScaleTransform(0.83, 0.83);

        private sealed class KeyCap
        {
            internal Border Surface;
            internal TextBlock Label;
            internal TextBlock ShiftLabel;
            internal Border Mark;
            internal int Finger;
            internal char Key;
        }

        private sealed class Cell
        {
            internal Border Surface;
            internal TextBlock Label;
        }

        private sealed class LessonRow
        {
            internal Button Button;
            internal Border NumberTile;
            internal TextBlock Number;
            internal TextBlock Keys;
        }

        private readonly Window window;
        private readonly ProgressStore store;
        private readonly GuidePreferences guides;
        private readonly IWordSpeech speech;
        private readonly Grid root;
        private readonly Button russianTab;
        private readonly Button englishTab;
        private readonly Button freeButton;
        private readonly Button practiceTab;
        private readonly Button demoTab;
        private readonly Button repeatSentence;
        private readonly Button action;
        private readonly Button reset;
        private readonly Button finish;
        private readonly Button homePositionButton;
        private readonly ToggleButton keyboardToggle;
        private readonly ToggleButton fingersToggle;
        private readonly ToggleButton waitAfterError;
        private readonly ToggleButton audioToggle;
        private readonly HandGuide handGuide;
        private readonly Button[] sectionButtons;
        private readonly Button[] modeButtons;
        private readonly StackPanel passageBox;
        private readonly DispatcherTimer statsTimer;
        private readonly DispatcherTimer demoTimer;
        private readonly DispatcherTimer nextSentenceTimer;
        private readonly Random random = new Random();
        private readonly List<Cell> passageCells = new List<Cell>();
        private readonly List<LessonRow> lessonRows = new List<LessonRow>();
        private readonly Dictionary<char, KeyCap> keys = new Dictionary<char, KeyCap>();
        private readonly Dictionary<char, KeyCap> shiftedKeys = new Dictionary<char, KeyCap>();
        private KeyCap leftShift;
        private KeyCap rightShift;
        private Exercise exercise;
        private SessionReport report;
        private Course course;
        private int lessonIndex;
        private LessonSection section;
        private PracticeMode mode;
        private bool demo;
        private bool dictation { get { return exercise != null && exercise.IsDictation; } }
        private bool audioAvailable;
        private bool showHomePosition;
        private bool hasError;
        private bool advancedAfterError;
        private bool wrongLayout;
        private bool saved = true;

        internal TrainerWindow(Window window, ProgressStore store, GuidePreferences guides = null, IWordSpeech speech = null)
        {
            this.window = window;
            this.store = store;
            this.guides = guides ?? new GuidePreferences();
            this.speech = speech ?? new WordSpeech();
            Text = ThemeBrush("Text");
            Muted = ThemeBrush("Muted");
            Pending = ThemeBrush("PendingText");
            Accent = ThemeBrush("Accent");
            AccentInk = ThemeBrush("AccentInk");
            KeyFill = ThemeBrush("ControlFill");
            KeyOutline = ThemeBrush("ControlBorder");
            TabFill = ThemeBrush("SelectedFill");
            TabOutline = ThemeBrush("SelectedBorder");
            SelectedLesson = ThemeBrush("SelectedLesson");

            using (Stream brand = Assembly.GetExecutingAssembly().GetManifestResourceStream("RhythmTyping.Brand.png"))
            {
                BitmapFrame icon = BitmapFrame.Create(brand, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                window.Icon = icon;
                Find<Image>("BrandIcon").Source = icon;
            }

            root = Find<Grid>("Root");
            Write("FooterVersion", "Версия " + App.Version);
            russianTab = Find<Button>("RussianTab");
            englishTab = Find<Button>("EnglishTab");
            freeButton = Find<Button>("FreeButton");
            practiceTab = Find<Button>("PracticeTab");
            demoTab = Find<Button>("DemoTab");
            repeatSentence = Find<Button>("RepeatSentence");
            action = Find<Button>("ActionButton");
            reset = Find<Button>("ResetButton");
            finish = Find<Button>("FinishButton");
            homePositionButton = Find<Button>("HomePositionButton");
            keyboardToggle = Find<ToggleButton>("KeyboardToggle");
            fingersToggle = Find<ToggleButton>("FingersToggle");
            waitAfterError = Find<ToggleButton>("WaitAfterError");
            audioToggle = Find<ToggleButton>("AudioToggle");
            keyboardToggle.IsChecked = this.guides.KeyboardVisible;
            fingersToggle.IsChecked = this.guides.FingersVisible;
            handGuide = new HandGuide(Find<Canvas>("LeftHand"), Find<Canvas>("RightHand"));
            sectionButtons = new[] { Find<Button>("LettersSection"), Find<Button>("WordsSection"),
                Find<Button>("SymbolsSection"), Find<Button>("TextsSection") };
            modeButtons = new[] { Find<Button>("LessonMode"), Find<Button>("UnlimitedMode"), Find<Button>("FiveMode"), Find<Button>("TenMode") };
            passageBox = Find<StackPanel>("PassageBox");

            statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            statsTimer.Tick += delegate
            {
                if (exercise.Tick()) FinishSession();
                else
                {
                    UpdateMetrics();
                    if (exercise.IsTimed) UpdateMeters();
                }
            };
            demoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
            demoTimer.Tick += delegate
            {
                exercise.DemoStep();
                if (exercise.Phase == Phase.Finished) StopTimers();
                Refresh();
            };
            nextSentenceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(WordSpeech.ErrorSoundMilliseconds + 30) };
            nextSentenceTimer.Tick += delegate { SpeakCurrentSentence(); };

            russianTab.Click += delegate { SelectCourse(Catalog.Russian); };
            englishTab.Click += delegate { SelectCourse(Catalog.English); };
            freeButton.Click += delegate { SelectLesson(-1); };
            practiceTab.Click += delegate { SelectSession(false); };
            demoTab.Click += delegate { SelectSession(true); };
            repeatSentence.Click += delegate { SpeakCurrentSentence(); root.Focus(); };
            action.Click += delegate { PrimaryAction(); };
            reset.Click += delegate { Reset(); };
            Find<Button>("ExportButton").Click += delegate { ExportReport(); };
            homePositionButton.Click += delegate { ToggleHomePosition(); };
            keyboardToggle.Checked += GuideVisibilityChanged;
            keyboardToggle.Unchecked += GuideVisibilityChanged;
            fingersToggle.Checked += GuideVisibilityChanged;
            fingersToggle.Unchecked += GuideVisibilityChanged;
            waitAfterError.Checked += ErrorPolicyChanged;
            waitAfterError.Unchecked += ErrorPolicyChanged;
            audioToggle.Checked += AudioChanged;
            audioToggle.Unchecked += AudioChanged;
            finish.Click += delegate { if (exercise.FinishUnlimited()) FinishSession(); };
            for (int index = 0; index < sectionButtons.Length; index++)
            {
                Button button = sectionButtons[index];
                button.Tag = (LessonSection)index;
                button.Click += delegate { SelectSection((LessonSection)button.Tag); };
            }
            for (int index = 0; index < modeButtons.Length; index++)
            {
                modeButtons[index].Tag = (PracticeMode)index;
                modeButtons[index].Click += delegate(object sender, RoutedEventArgs args) { SelectMode((PracticeMode)((Button)sender).Tag); };
            }
            passageBox.MouseLeftButtonDown += delegate { root.Focus(); };
            window.PreviewKeyDown += OnKeyDown;
            window.PreviewTextInput += OnTextInput;
            window.Deactivated += delegate { Pause(); };
            window.Closed += delegate { StopTimers(); this.speech.Dispose(); };
            window.Loaded += delegate { root.Focus(); RevealLesson(); UpdateMeters(); };
            root.SizeChanged += delegate { UpdateDensity(); UpdateMeters(); };
            passageBox.SizeChanged += delegate { UpdateMeters(); };

            SelectCourse(Catalog.Russian);
        }

        private T Find<T>(string name) where T : class
        {
            return (T)window.FindName(name);
        }

        private Brush ThemeBrush(string key)
        {
            return (Brush)window.FindResource(key);
        }

        private void Write(string name, string value)
        {
            Find<TextBlock>(name).Text = value;
        }

        private void UpdateDensity()
        {
            StackPanel main = Find<StackPanel>("MainContent");
            bool compact = root.ActualHeight < 715;
            main.LayoutTransform = compact ? CompactContent : Transform.Identity;
            main.Margin = compact ? new Thickness(17, 9, 17, 6) : new Thickness(24, 16, 24, 10);
            UpdateGuideLayout();
        }

        private void GuideVisibilityChanged(object sender, RoutedEventArgs args)
        {
            guides.KeyboardVisible = keyboardToggle.IsChecked == true;
            guides.FingersVisible = fingersToggle.IsChecked == true;
            if (!guides.FingersVisible) showHomePosition = false;
            UpdateGuideLayout();
            UpdateKeyboard();
            root.Focus();
            guides.Save();
        }

        private void UpdateGuideLayout()
        {
            bool keyboard = guides.KeyboardVisible;
            bool fingers = guides.FingersVisible;
            bool both = keyboard && fingers;
            double handWidth = root.ActualWidth > 0 && root.ActualWidth < 1040 ? 173 : 210;
            keyboardToggle.ToolTip = keyboard ? "Скрыть экранную клавиатуру" : "Показать экранную клавиатуру";
            fingersToggle.ToolTip = fingers ? "Скрыть подсказки по пальцам" : "Показать подсказки по пальцам";
            Find<StackPanel>("GuideToggles").Visibility = report == null ? Visibility.Visible : Visibility.Collapsed;
            audioToggle.Visibility = demo ? Visibility.Collapsed : Visibility.Visible;
            audioToggle.ToolTip = dictation ? "Выключить озвучку текста" : "Включить озвучку текста по предложениям";
            repeatSentence.Visibility = dictation ? Visibility.Visible : Visibility.Collapsed;
            Find<Border>("KeyboardPanel").Visibility = report == null && (keyboard || fingers) ? Visibility.Visible : Visibility.Collapsed;
            Find<Border>("KeyboardDeck").Visibility = keyboard ? Visibility.Visible : Visibility.Collapsed;
            Visibility fingerVisibility = fingers ? Visibility.Visible : Visibility.Collapsed;
            var hands = Find<Border>("HandsPanel");
            hands.Visibility = fingerVisibility;
            Grid.SetColumn(hands, keyboard ? 2 : 0);
            hands.Width = keyboard ? Double.NaN : handWidth;
            hands.HorizontalAlignment = keyboard ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            Find<ColumnDefinition>("GuideGap").Width = new GridLength(both ? 9 : 0);
            Find<ColumnDefinition>("HandsColumn").Width = new GridLength(both ? handWidth : 0);
            homePositionButton.Visibility = fingerVisibility;
            Find<TextBlock>("FingerLabel").Visibility = fingerVisibility;
            Find<TextBlock>("Instruction").Visibility = fingerVisibility;
            Write("GuideTitle", both ? "Клавиатура и пальцы" : keyboard ? "Экранная клавиатура" : "Подсказки по пальцам");
        }

        private void BuildLessonList()
        {
            StackPanel list = Find<StackPanel>("LessonList");
            list.Children.Clear();
            lessonRows.Clear();
            for (int index = 0; index < course.Lessons.Length; index++)
            {
                Lesson lesson = course.Lessons[index];
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                TextBlock number = new TextBlock { FontSize = 11,
                    FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center };
                Border tile = new Border { Width = 27, Height = 27, CornerRadius = new CornerRadius(7),
                    Child = number, HorizontalAlignment = HorizontalAlignment.Left };
                row.Children.Add(tile);

                StackPanel labels = new StackPanel { Margin = new Thickness(7, 0, 0, 0) };
                labels.Children.Add(new TextBlock { Text = lesson.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                TextBlock keyList = new TextBlock { Text = lesson.Keys, FontSize = 10, Margin = new Thickness(0, 1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                labels.Children.Add(keyList);
                Grid.SetColumn(labels, 1);
                row.Children.Add(labels);

                Button button = new Button { Style = (Style)window.FindResource("FlatButton"), Content = row,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(7, 5, 7, 5),
                    Height = 45, Margin = new Thickness(0, 0, 0, 3), Tag = index };
                button.Click += delegate(object sender, RoutedEventArgs args) { SelectLesson((int)((Button)sender).Tag); };
                list.Children.Add(button);
                lessonRows.Add(new LessonRow { Button = button, NumberTile = tile, Number = number, Keys = keyList });
            }
        }

        private void BuildKeyboard()
        {
            StackPanel panel = Find<StackPanel>("KeyboardBox");
            panel.Children.Clear();
            keys.Clear();
            shiftedKeys.Clear();
            bool english = course.English;
            StackPanel numbers = AddKeyRow(panel);
            AddLetters(numbers, english ? "`1234567890-=" : "ё1234567890-=", new[] { 0, 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7 },
                english ? "~!@#$%^&*()_+" : "Ё!\"№;%:?*()_+");
            AddKey(numbers, "Backspace", '\0', 2, -1);

            StackPanel top = AddKeyRow(panel);
            AddKey(top, "Tab", '\0', 1.5, -1);
            AddLetters(top, english ? "qwertyuiop[]" : "йцукенгшщзхъ", new[] { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7 },
                english ? "QWERTYUIOP{}" : null);
            AddKey(top, "\\", '\\', 1.5, 7, english ? '|' : '/');

            StackPanel home = AddKeyRow(panel);
            AddKey(home, "Caps Lock", '\0', 1.75, -1);
            AddLetters(home, english ? "asdfghjkl;'" : "фывапролджэ", new[] { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7 },
                english ? "ASDFGHJKL:\"" : null);
            AddKey(home, "Enter", '\0', 2.25, -1);

            StackPanel bottom = AddKeyRow(panel);
            leftShift = AddKey(bottom, "Shift", '\0', 2.25, 0);
            AddLetters(bottom, english ? "zxcvbnm,./" : "ячсмитьбю.", new[] { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7 },
                english ? "ZXCVBNM<>?" : "ЯЧСМИТЬБЮ,");
            rightShift = AddKey(bottom, "Shift", '\0', 2.75, 7);

            StackPanel modifiers = AddKeyRow(panel);
            AddKey(modifiers, "Ctrl", '\0', 1.35, -1);
            AddKey(modifiers, "⊞", '\0', 1.2, -1);
            AddKey(modifiers, "Alt", '\0', 1.2, -1);
            AddKey(modifiers, "Пробел", ' ', 7.5, HandGuide.Thumb);
            AddKey(modifiers, "Alt", '\0', 1.2, -1);
            AddKey(modifiers, "⊞", '\0', 1.2, -1);
            AddKey(modifiers, "Ctrl", '\0', 1.35, -1);
        }

        private static StackPanel AddKeyRow(Panel panel)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(row);
            return row;
        }

        private void AddLetters(StackPanel row, string letters, int[] fingers, string shift = null)
        {
            for (int index = 0; index < letters.Length; index++)
            {
                char letter = letters[index];
                char alternate = shift == null ? Char.ToUpperInvariant(letter) : shift[index];
                AddKey(row, Char.ToUpperInvariant(letter).ToString(), letter, 1, fingers[index], alternate);
            }
        }

        private KeyCap AddKey(Panel row, string caption, char key, double width, int finger, char alternate = '\0')
        {
            bool characterKey = width == 1;
            Grid face = new Grid();
            TextBlock label = new TextBlock { Text = caption, FontSize = characterKey ? 17 : 13,
                FontWeight = FontWeights.SemiBold, Foreground = key == '\0' ? Muted : Text,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            face.Children.Add(label);

            TextBlock shiftLabel = new TextBlock { FontSize = 9, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 4, 0) };
            if (alternate != '\0' && alternate.ToString() != caption) shiftLabel.Text = alternate.ToString();
            face.Children.Add(shiftLabel);

            Border mark = new Border { Width = key == 'а' || key == 'о' || key == 'f' || key == 'j' ? 13 : 4, Height = 3,
                CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 4, 4),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            if (finger >= 0)
            {
                mark.Background = HandGuide.BrightColor(finger);
                face.Children.Add(mark);
            }

            Border surface = new Border { Background = KeyFill, BorderBrush = KeyOutline, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Width = 49.6 * width - 4, Height = 34,
                Margin = new Thickness(2, 0, 2, 4), Child = face };
            row.Children.Add(surface);
            KeyCap cap = new KeyCap { Surface = surface, Label = label, ShiftLabel = shiftLabel, Mark = mark, Finger = finger, Key = key };
            if (key != '\0') keys.Add(key, cap);
            if (alternate != '\0') shiftedKeys.Add(alternate, cap);
            return cap;
        }

        private bool IsFreePractice { get { return lessonIndex < 0; } }
        private Lesson CurrentLesson { get { return IsFreePractice ? course.FreePractice : course.Lessons[lessonIndex]; } }

        private string RecordKey(Lesson lesson, PracticeMode practiceMode, InputActivity activity = InputActivity.Typing)
        {
            string lessonKey = course.Code + ":" + lesson.Id;
            string modeKey = practiceMode == PracticeMode.Lesson ? lessonKey : lessonKey + ":" + practiceMode;
            return activity == InputActivity.Typing ? modeKey : modeKey + ":" + activity.ToString().ToLowerInvariant();
        }

        private void SelectCourse(Course selected)
        {
            if (course == selected)
            {
                root.Focus();
                return;
            }
            LessonSection? preferredSection = course == null || IsFreePractice ? (LessonSection?)null : section;
            course = selected;
            lessonIndex = FirstLessonToPractice(preferredSection);
            section = CurrentLesson.Section;
            mode = PracticeMode.Lesson;
            demo = false;
            BuildLessonList();
            BuildKeyboard();
            Reset();
            Find<ScrollViewer>("LessonScroll").ScrollToTop();
            RevealLesson();
        }

        private int FirstLessonToPractice(LessonSection? filter)
        {
            int first = -1;
            for (int index = 0; index < course.Lessons.Length; index++)
            {
                Lesson lesson = course.Lessons[index];
                if (filter.HasValue && lesson.Section != filter.Value) continue;
                if (first < 0) first = index;
                if (store.For(RecordKey(lesson, PracticeMode.Lesson)).Attempts == 0) return index;
            }
            return Math.Max(0, first);
        }

        private void SelectSection(LessonSection selected)
        {
            if (section != selected || IsFreePractice) SelectLesson(FirstLessonToPractice(selected));
            else root.Focus();
        }

        private void SelectLesson(int index)
        {
            if (lessonIndex != index)
            {
                bool wasFree = IsFreePractice;
                LessonSection previousSection = section;
                lessonIndex = index;
                if (IsFreePractice)
                {
                    demo = false;
                    mode = PracticeMode.Unlimited;
                }
                else
                {
                    section = CurrentLesson.Section;
                    if (wasFree) mode = PracticeMode.Lesson;
                }
                Reset();
                if (previousSection != section) Find<ScrollViewer>("LessonScroll").ScrollToTop();
                RevealLesson();
            }
            else root.Focus();
        }

        private void RevealLesson()
        {
            if (IsFreePractice || !window.IsLoaded) return;
            lessonRows[lessonIndex].Button.BringIntoView();
        }

        private void SelectMode(PracticeMode selected)
        {
            if (mode != selected)
            {
                mode = selected;
                Reset();
            }
            else root.Focus();
        }

        private void SelectSession(bool isDemo)
        {
            if (demo != isDemo)
            {
                demo = isDemo;
                Reset();
            }
            else root.Focus();
        }

        private void Reset()
        {
            StopTimers();
            Lesson lesson = CurrentLesson;
            exercise = new Exercise(lesson, random, mode, demo, advanceOnError: waitAfterError.IsChecked != true, isDictation: audioToggle.IsChecked == true);
            audioAvailable = !dictation || speech.Prepare(course.English);
            ShowReport(null);
            hasError = false;
            advancedAfterError = false;
            wrongLayout = false;
            saved = true;
            Write("LessonTitle", lesson.Title);
            SetTab(russianTab, !course.English);
            SetTab(englishTab, course.English);
            SetTab(freeButton, IsFreePractice);
            if (!IsFreePractice)
            {
                freeButton.Background = KeyFill;
                freeButton.BorderBrush = KeyOutline;
            }
            SetTab(practiceTab, !demo);
            SetTab(demoTab, demo);
            Find<Border>("SessionTabs").Visibility = IsFreePractice ? Visibility.Collapsed : Visibility.Visible;
            Find<StackPanel>("ModeRow").Visibility = demo ? Visibility.Collapsed : Visibility.Visible;
            waitAfterError.Visibility = demo ? Visibility.Collapsed : Visibility.Visible;
            modeButtons[0].Visibility = IsFreePractice ? Visibility.Collapsed : Visibility.Visible;
            for (int index = 0; index < modeButtons.Length; index++) SetTab(modeButtons[index], (PracticeMode)index == mode);
            Write("TimeLabel", exercise.IsTimed ? "ОСТАЛОСЬ" : "ВРЕМЯ");
            UpdateLessons();
            BuildPassage();
            Refresh();
            root.Focus();
        }

        private void ErrorPolicyChanged(object sender, RoutedEventArgs args)
        {
            exercise.SetErrorPolicy(waitAfterError.IsChecked != true);
            UpdateErrorPolicyControl();
            root.Focus();
        }

        private void UpdateErrorPolicyControl()
        {
            bool ready = exercise.Phase == Phase.Ready;
            waitAfterError.IsEnabled = ready;
            string policy = exercise.AdvanceOnError ? "После ошибки курсор переходит к следующему знаку." : "После ошибки тренажёр ждёт правильного знака.";
            AutomationProperties.SetName(waitAfterError, policy.TrimEnd('.'));
            waitAfterError.ToolTip = policy + (ready ? " Нажмите, чтобы сменить режим." : " Режим выбирается перед началом.");
        }

        private void AudioChanged(object sender, RoutedEventArgs args)
        {
            if (exercise == null || demo || exercise.Phase == Phase.Finished) return;
            exercise.SetDictation(audioToggle.IsChecked == true);
            speech.Stop();
            audioAvailable = !dictation || speech.Prepare(course.English);
            showHomePosition = false;
            UpdateGuideLayout();
            BuildPassage();
            if (!audioAvailable && exercise.Phase == Phase.Running) Pause();
            else
            {
                Refresh();
                SpeakCurrentSentence();
            }
            root.Focus();
        }

        private void BuildPassage()
        {
            passageCells.Clear();
            passageBox.Children.Clear();
            AutomationProperties.SetName(passageBox, dictation ? "Скрытый текст для набора на слух" : "Текст упражнения. " + exercise.Passage);
            if (exercise.Verse == null)
            {
                AddPassageLine(exercise.Passage);
                return;
            }

            string[] lines = exercise.Verse.Lines;
            for (int index = 0; index < lines.Length; index++)
            {
                bool separator = index < lines.Length - 1 || exercise.Passage.EndsWith(" ", StringComparison.Ordinal);
                AddPassageLine(lines[index] + (separator ? " " : ""));
            }
        }

        private void AddPassageLine(string line)
        {
            var row = new WrapPanel();
            passageBox.Children.Add(row);
            string[] words = line.Split(' ');
            for (int index = 0; index < words.Length; index++)
            {
                StackPanel word = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(word);
                string characters = index == words.Length - 1 ? words[index] : words[index] + " ";
                foreach (char character in characters)
                {
                    TextBlock label = new TextBlock { Text = dictation && character != ' ' ? "•" : character.ToString(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Border surface = new Border { Width = 15, Height = 29, CornerRadius = new CornerRadius(3), Child = label };
                    passageCells.Add(new Cell { Surface = surface, Label = label });
                    word.Children.Add(surface);
                }
            }
        }

        private void PrimaryAction()
        {
            if (exercise.Phase == Phase.Running)
            {
                Pause();
                return;
            }
            if (exercise.Phase == Phase.Finished)
            {
                if (CanAdvanceLesson())
                {
                    SelectLesson(lessonIndex + 1);
                    return;
                }
                Reset();
            }
            Start();
        }

        private bool CanAdvanceLesson()
        {
            return !demo && exercise.Activity == InputActivity.Typing && !IsFreePractice && mode == PracticeMode.Lesson && lessonIndex + 1 < course.Lessons.Length;
        }

        private void Start()
        {
            audioAvailable = !dictation || speech.Prepare(course.English);
            if (!audioAvailable)
            {
                UpdateStatus();
                return;
            }
            showHomePosition = false;
            exercise.Start();
            if (demo) demoTimer.Start();
            else statsTimer.Start();
            root.Focus();
            Refresh();
            SpeakCurrentSentence();
        }

        private void SpeakCurrentSentence()
        {
            nextSentenceTimer.Stop();
            if (!dictation || !audioAvailable || exercise.Phase == Phase.Finished) return;
            audioAvailable = speech.Speak(WordSpeech.SentencePrompt(exercise.TextToDictate(), course.English));
            if (!audioAvailable && exercise.Phase == Phase.Running) Pause();
            else UpdateStatus();
        }

        private void ToggleHomePosition()
        {
            showHomePosition = !showHomePosition;
            if (showHomePosition && exercise.Phase == Phase.Running) Pause();
            else UpdateKeyboard();
            root.Focus();
        }

        private void Pause()
        {
            if (exercise.Phase != Phase.Running) return;
            if (exercise.Tick())
            {
                FinishSession();
                return;
            }
            exercise.Pause();
            StopTimers();
            Refresh();
        }

        private void StopTimers()
        {
            statsTimer.Stop();
            demoTimer.Stop();
            nextSentenceTimer.Stop();
            speech.Stop();
        }

        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.F4 && dictation && Keyboard.Modifiers == ModifierKeys.None)
            {
                args.Handled = true;
                if (!args.IsRepeat) SpeakCurrentSentence();
                return;
            }
            if (args.Key == Key.Escape && exercise.Phase == Phase.Running)
            {
                args.Handled = true;
                Pause();
                return;
            }
            if (args.Key != Key.Space || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
            if (Keyboard.FocusedElement is ButtonBase) return;
            args.Handled = true;
            if (args.IsRepeat) return;
            if (exercise.Phase == Phase.Ready || exercise.Phase == Phase.Paused) Start();
            else if (exercise.Phase == Phase.Running && !demo) Submit(' ');
            else if (exercise.Phase == Phase.Finished) PrimaryAction();
        }

        private void OnTextInput(object sender, TextCompositionEventArgs args)
        {
            if (exercise.Phase != Phase.Running || demo || Keyboard.FocusedElement is ButtonBase) return;
            args.Handled = true;
            foreach (char character in args.Text)
            {
                if (exercise.Phase != Phase.Running) break;
                if (!Char.IsControl(character)) Submit(character);
            }
        }

        private void Submit(char character)
        {
            int previousDictationEnd = dictation && exercise.Current.HasValue ? exercise.DictationEnd : Int32.MaxValue;
            int mistakesBefore = exercise.Mistakes;
            InputResult result = exercise.Type(character);
            bool mistake = exercise.Mistakes > mistakesBefore;
            hasError = mistake && !exercise.AdvanceOnError;
            advancedAfterError = mistake && exercise.AdvanceOnError;
            wrongLayout = mistake && TypingMistake.IsWrongLayout(character, course.English);
            if (mistake) speech.SignalError();
            if (result == InputResult.Finished)
            {
                FinishSession();
                return;
            }
            if (result == InputResult.NextPage) BuildPassage();
            Refresh();
            if (dictation && (result == InputResult.NextPage || exercise.Position >= previousDictationEnd))
            {
                if (mistake)
                {
                    speech.Stop();
                    nextSentenceTimer.Stop();
                    nextSentenceTimer.Start();
                }
                else SpeakCurrentSentence();
            }
        }

        private void FinishSession()
        {
            StopTimers();
            hasError = false;
            advancedAfterError = false;
            wrongLayout = false;
            if (exercise.Typed > 0) saved = store.Record(RecordKey(CurrentLesson, mode, exercise.Activity), exercise.Speed, exercise.Accuracy);
            ShowReport(new SessionReport(exercise, course, CurrentLesson));
            UpdateLessons();
            Refresh();
            root.Focus();
        }

        private void ShowReport(SessionReport result)
        {
            bool returningToPractice = report != null && result == null;
            report = result;
            bool open = result != null;
            Find<Border>("ReportPanel").Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            Find<StackPanel>("PracticeDetails").Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            UpdateGuideLayout();
            Find<Grid>("SessionActionRow").Margin = open ? new Thickness(0) : new Thickness(0, 9, 0, 0);
            Find<DataGrid>("MistakeTable").ItemsSource = open ? result.Errors : null;
            Find<TextBlock>("ExportStatus").Visibility = Visibility.Collapsed;
            if (open)
            {
                Write("TimeLabel", "ВРЕМЯ");
                Write("ReportSummary", result.Summary);
                Write("ReportRecommendations", String.Join("\n", result.Recommendations));
                Find<StackPanel>("ReportErrors").Visibility = result.Errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (open || returningToPractice) Find<ScrollViewer>("MainScroll").ScrollToTop();
        }

        private void ExportReport()
        {
            if (report == null) return;
            var dialog = new SaveFileDialog
            {
                FileName = report.FileName,
                Filter = "Markdown (*.md)|*.md",
                DefaultExt = ".md",
                AddExtension = true,
                OverwritePrompt = true,
                Title = "Сохранить отчёт о тренировке"
            };
            if (dialog.ShowDialog(window) != true) return;
            TextBlock status = Find<TextBlock>("ExportStatus");
            status.Visibility = Visibility.Visible;
            try
            {
                report.SaveMarkdown(dialog.FileName);
                status.Foreground = Accent;
                status.Text = "Отчёт сохранён. " + Path.GetFileName(dialog.FileName);
                status.ToolTip = dialog.FileName;
            }
            catch (IOException) { ShowExportError(status); }
            catch (UnauthorizedAccessException) { ShowExportError(status); }
        }

        private void ShowExportError(TextBlock status)
        {
            status.Foreground = Pink;
            status.Text = "Не удалось сохранить отчёт. Попробуйте выбрать другую папку.";
            status.ToolTip = null;
        }

        private void Refresh()
        {
            Write("PassageLabel", dictation ? "Набор на слух" : exercise.Verse != null ? exercise.Verse.Title : demo ? "Показ набора" : "Текст для набора");
            if (exercise.PageCount == 1 && (demo || exercise.Verse == null))
                Write("PositionLabel", String.Format("{0} / {1} зн.", exercise.Position, exercise.Passage.Length));
            else if (exercise.PageCount > 1)
                Write("PositionLabel", String.Format("Фрагмент {0} из {1}  ·  {2} зн.", exercise.Page, exercise.PageCount, exercise.Processed));
            else
                Write("PositionLabel", String.Format("Фрагмент {0}  ·  {1} зн.", exercise.Page, exercise.Processed));
            UpdatePassage();
            UpdateMetrics();
            UpdateKeyboard();
            UpdateStatus();
            UpdateMeters();
        }

        private void UpdateLessons()
        {
            int complete = 0;
            int best = 0;
            int inSection = 0;
            int completeInSection = 0;
            int selectedNumber = 0;
            for (int index = 0; index < lessonRows.Count; index++)
            {
                LessonRow row = lessonRows[index];
                Lesson lesson = course.Lessons[index];
                LessonRecord record = store.For(RecordKey(lesson, PracticeMode.Lesson));
                bool passed = record.Attempts > 0;
                if (passed) complete++;
                best = Math.Max(best, record.BestSpeed);
                bool visible = lesson.Section == section;
                row.Button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (!visible) continue;

                inSection++;
                if (passed) completeInSection++;
                bool selected = index == lessonIndex;
                if (selected) selectedNumber = inSection;
                row.Number.Text = passed ? "✓" : inSection.ToString("00");
                row.Number.Foreground = selected || passed ? Accent : Muted;
                row.NumberTile.Background = selected || passed ? TabFill : KeyFill;
                row.Keys.Foreground = selected ? Accent : Muted;
                row.Button.Background = selected ? SelectedLesson : Transparent;
                row.Button.BorderBrush = selected ? TabOutline : Transparent;
                AutomationProperties.SetName(row.Button, String.Format("Урок {0}. {1}{2}", inSection, lesson.Title,
                    passed ? ". Пройден" : ""));
            }

            for (int index = 0; index < sectionButtons.Length; index++) SetTab(sectionButtons[index], index == (int)section);
            string layout = course.English ? "АНГЛИЙСКАЯ" : "РУССКАЯ";
            string sectionName = sectionButtons[(int)section].Content.ToString();
            Write("LessonNumber", IsFreePractice ? layout + " РАСКЛАДКА" : String.Format("{0}  /  {1}  /  УРОК {2:00} ИЗ {3:00}",
                layout, sectionName.ToUpperInvariant(), selectedNumber, inSection));
            Write("SectionCount", String.Format("{0} / {1}", completeInSection, inSection));
            Write("CourseCount", String.Format("{0} / {1}", complete, lessonRows.Count));
            Write("PersonalBest", best > 0 ? String.Format("Рекорд уроков  {0} зн/мин", best) : "Рекорд уроков  —");
            Border courseMeter = Find<Border>("CourseMeter");
            courseMeter.Tag = (double)complete / lessonRows.Count;
        }

        private void SetTab(Button tab, bool active)
        {
            tab.Foreground = active ? Accent : Muted;
            tab.Background = active ? TabFill : Transparent;
            tab.BorderBrush = active ? TabOutline : Transparent;
        }

        private void UpdatePassage()
        {
            for (int index = 0; index < passageCells.Count; index++)
            {
                Cell cell = passageCells[index];
                bool current = index == exercise.Position;
                bool passed = index < exercise.Position;
                bool space = exercise.Passage[index] == ' ';
                bool error = exercise.WasSkipped(index) || (current && hasError);
                string label = space && (current || error) ? "·" : exercise.Passage[index].ToString();
                if (dictation && !space && (!passed || error)) label = error ? "×" : "•";
                cell.Label.Text = label;
                cell.Label.Foreground = error ? Pink : current ? AccentInk : passed ? Accent : Pending;
                cell.Surface.Background = error ? ErrorFill : current ? Accent : Transparent;
            }
        }

        private void UpdateMetrics()
        {
            bool show = !demo && (exercise.Typed > 0 || exercise.Mistakes > 0);
            Write("SpeedValue", show ? exercise.Speed.ToString() : "—");
            Write("AccuracyValue", show ? exercise.Accuracy + "%" : "—");
            Write("MistakesValue", show ? exercise.Mistakes.ToString() : "—");
            int seconds = exercise.IsTimed && report == null ? (int)Math.Ceiling(exercise.Remaining.TotalSeconds) : (int)exercise.Elapsed.TotalSeconds;
            Write("TimeValue", String.Format("{0:00}:{1:00}", seconds / 60, seconds % 60));
        }

        private void UpdateMeters()
        {
            if (exercise == null) return;
            Border courseMeter = Find<Border>("CourseMeter");
            Border current = Find<Border>("ExerciseMeter");
            courseMeter.Width = ((FrameworkElement)courseMeter.Parent).ActualWidth * (double)courseMeter.Tag;
            double pageProgress = (double)exercise.Position / exercise.Passage.Length;
            double progress = exercise.IsTimed ? exercise.Elapsed.TotalSeconds / exercise.Limit.TotalSeconds
                : exercise.PageCount > 0 ? (exercise.Page - 1 + pageProgress) / exercise.PageCount : pageProgress;
            current.Width = ((FrameworkElement)current.Parent).ActualWidth * progress;
        }

        private void UpdateKeyboard()
        {
            foreach (KeyCap cap in keys.Values) PaintKey(cap, KeyStyle.Normal);
            PaintKey(leftShift, KeyStyle.Normal);
            PaintKey(rightShift, KeyStyle.Normal);

            string homeKeys = HandGuide.HomeKeys(course.English);
            var hint = Find<TextBlock>("FingerLabel");
            homePositionButton.Background = showHomePosition ? TabFill : KeyFill;
            homePositionButton.BorderBrush = showHomePosition ? TabOutline : KeyOutline;
            homePositionButton.Foreground = showHomePosition ? Accent : Text;
            homePositionButton.Content = showHomePosition ? "Скрыть позицию" : "Исходная позиция";
            homePositionButton.ToolTip = showHomePosition ? "Показать подсказку для следующего знака" : "Показать, куда положить пальцы перед набором";

            if (showHomePosition)
            {
                foreach (char key in homeKeys) PaintKey(keys[key], KeyStyle.Home);
                PaintKey(keys[' '], KeyStyle.Home);
                handGuide.Show(homeKeys, -1, -1, true, false);
                hint.Foreground = Accent;
                hint.Text = HomeKeyCaption(homeKeys.Substring(0, 4)) + "    ·    " + HomeKeyCaption(homeKeys.Substring(4));
                Write("Instruction", course.English
                    ? "Указательные — на выступах F и J. Остальные — на соседних клавишах. Большие — над пробелом."
                    : "Указательные — на выступах А и О. Остальные — на соседних клавишах. Большие — над пробелом.");
                return;
            }

            Write("Instruction", CurrentLesson.Instruction);
            char character = exercise.Current.GetValueOrDefault();
            KeyCap current;
            bool shift = shiftedKeys.TryGetValue(character, out current);
            if (!shift && !keys.TryGetValue(character, out current))
            {
                handGuide.Show(homeKeys, -1, -1, false, false);
                hint.Foreground = Muted;
                hint.Text = "Верните пальцы на исходный ряд";
                return;
            }

            KeyCap shiftKey = shift ? (current.Finger <= 3 ? rightShift : leftShift) : null;
            KeyStyle style = hasError ? KeyStyle.Error : KeyStyle.Active;
            PaintKey(current, style);
            if (shiftKey != null) PaintKey(shiftKey, style);
            handGuide.Show(homeKeys, current.Finger, shiftKey == null ? -1 : shiftKey.Finger, false, hasError);
            hint.Foreground = hasError ? HandGuide.ErrorInk : HandGuide.BrightColor(current.Finger);
            string next = character == ' ' ? "Пробел" : character.ToString();
            string finger = character == ' ' ? "Любой большой палец" : HandGuide.FingerNames[current.Finger];
            string shiftHint = !shift ? "" : shiftKey == leftShift ? " + левый Shift" : " + правый Shift";
            hint.Text = next + "  ·  " + finger + shiftHint;
        }

        private static string HomeKeyCaption(string keys)
        {
            return String.Join(" ", keys.ToUpperInvariant().ToCharArray());
        }

        private enum KeyStyle { Normal, Home, Active, Error }

        private void PaintKey(KeyCap cap, KeyStyle style)
        {
            bool hasFinger = cap.Finger >= 0;
            Brush ink = cap.Key == '\0' && !hasFinger ? Muted : Text;
            Brush face = hasFinger && guides.FingersVisible ? HandGuide.KeyTint(cap.Finger) : KeyFill;
            Brush outline = KeyOutline;
            Brush mark = hasFinger && guides.FingersVisible ? HandGuide.BrightColor(cap.Finger) : Accent;
            if (style == KeyStyle.Error)
            {
                face = HandGuide.ErrorFill;
                ink = outline = mark = HandGuide.ErrorInk;
            }
            else if (style != KeyStyle.Normal)
            {
                face = style == KeyStyle.Home ? HandGuide.RestTint(cap.Finger) : mark;
                outline = mark;
                ink = mark = HandGuide.Ink;
            }

            cap.Surface.Background = face;
            cap.Surface.BorderBrush = outline;
            cap.Label.Foreground = ink;
            cap.ShiftLabel.Foreground = style == KeyStyle.Normal ? Muted : ink;
            if (hasFinger) cap.Mark.Background = guides.FingersVisible ? mark : Transparent;
        }

        private void UpdateStatus()
        {
            TextBlock status = Find<TextBlock>("StatusLabel");
            status.Foreground = hasError || advancedAfterError ? Pink : Muted;
            UpdateErrorPolicyControl();
            repeatSentence.IsEnabled = dictation && audioAvailable && exercise.Phase != Phase.Finished;
            bool inProgress = exercise.Phase == Phase.Running || exercise.Phase == Phase.Paused;
            reset.Content = inProgress ? "Сброс" : "Новый текст";
            finish.Visibility = !demo && mode == PracticeMode.Unlimited && inProgress ? Visibility.Visible : Visibility.Collapsed;
            Write("FooterStatus", saved ? "Прогресс хранится на этом ПК" : "Не удалось сохранить прогресс");
            switch (exercise.Phase)
            {
                case Phase.Ready:
                    LessonRecord best = store.For(RecordKey(CurrentLesson, mode, exercise.Activity));
                    status.Text = demo ? "Следите за подсветкой клавиш и подсказкой по пальцам."
                        : dictation ? "Прослушайте и наберите предложение. F4 повторяет текущий отрывок."
                        : best.Attempts > 0 ? String.Format("Рекорд  {0} зн/мин  ·  лучшая точность {1}%", best.BestSpeed, best.BestAccuracy)
                        : CurrentLesson.IsPoetry ? "Набирайте строки по порядку. Между строками нажимайте пробел."
                        : mode == PracticeMode.Lesson ? "Наберите три фрагмента. Текст обновляется автоматически."
                        : exercise.IsTimed ? "Набирайте текст, пока идёт обратный отсчёт."
                        : "Набирайте в своём темпе. Текст будет обновляться.";
                    action.Content = demo ? "Запустить показ" : mode == PracticeMode.Lesson ? "Начать урок" : "Начать набор";
                    break;
                case Phase.Running:
                    status.Text = demo ? "Показ идёт автоматически."
                        : wrongLayout ? (course.English ? "Переключитесь на английскую раскладку." : "Переключитесь на русскую раскладку.")
                        : hasError ? (dictation ? "Опечатка. Нажмите F4, чтобы повторить текущий отрывок." : "Опечатка. Наберите выделенный знак.")
                        : advancedAfterError ? "Опечатка отмечена. Продолжайте со следующего знака."
                        : dictation ? "Набирайте на слух. Следующее предложение прозвучит, когда вы закончите текущее."
                        : "Набирайте текст в своём темпе.";
                    action.Content = "Пауза";
                    break;
                case Phase.Paused:
                    status.Text = "Пауза. Пробел продолжит с этого места.";
                    action.Content = "Продолжить";
                    break;
                case Phase.Finished:
                    string outcome = exercise.IsTimed ? "Время вышло." : mode == PracticeMode.Lesson ? "Урок пройден." : "Практика завершена.";
                    status.Text = demo ? "Демонстрация завершена." : exercise.Typed == 0 ? outcome + (exercise.Mistakes == 0 ? " Текст не набран." : " Правильно набранных знаков нет.")
                        : outcome + (saved ? " Результат сохранён." : " Результат виден до закрытия окна.");
                    status.Foreground = Accent;
                    action.Content = !CanAdvanceLesson() ? "Начать заново"
                        : course.Lessons[lessonIndex + 1].Section == section ? "Следующий урок" : "Следующий раздел";
                    break;
            }
            if (dictation && !audioAvailable && (exercise.Phase == Phase.Ready || exercise.Phase == Phase.Paused))
            {
                status.Foreground = Pink;
                status.Text = course.English
                    ? "Английский голос Windows недоступен. Проверьте параметры речи и устройство вывода."
                    : "Русский голос Windows недоступен. Проверьте параметры речи и устройство вывода.";
            }
        }
    }
}
