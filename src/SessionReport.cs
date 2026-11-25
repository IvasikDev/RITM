using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RhythmTyping
{
    internal sealed class TypingMistake
    {
        internal int Number { get; private set; }
        internal int Page { get; private set; }
        internal int Position { get; private set; }
        internal int GlobalPosition { get; private set; }
        internal char Expected { get; private set; }
        internal char Entered { get; private set; }
        internal long ElapsedMilliseconds { get; private set; }
        public string Location { get { return Page + " · " + Position; } }
        public string ExpectedLabel { get { return Label(Expected); } }
        public string EnteredLabel { get { return Label(Entered); } }
        public string Context { get; private set; }

        internal TypingMistake(int number, int page, int index, int processed, string passage, char entered, TimeSpan elapsed)
        {
            const int radius = 12;
            Number = number;
            Page = page;
            Position = index + 1;
            GlobalPosition = processed + 1;
            Expected = passage[index];
            Entered = entered;
            ElapsedMilliseconds = (long)elapsed.TotalMilliseconds;
            int start = Math.Max(0, index - radius);
            int end = Math.Min(passage.Length, index + radius + 1);
            Context = (start > 0 ? "…" : "") + passage.Substring(start, index - start)
                + "[" + (Expected == ' ' ? "␣" : Expected.ToString()) + "]"
                + passage.Substring(index + 1, end - index - 1) + (end < passage.Length ? "…" : "");
        }

        internal static string Label(char character)
        {
            if (character == ' ') return "Пробел";
            if (Char.IsWhiteSpace(character) || Char.IsControl(character) || Char.IsSurrogate(character)) return CodePoint(character);
            return character.ToString();
        }

        internal static string CodePoint(char character)
        {
            return "U+" + ((int)character).ToString("X4", CultureInfo.InvariantCulture);
        }

        internal static bool IsWrongLayout(char character, bool english)
        {
            if (english) return character >= '\u0400' && character <= '\u04FF';
            return (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');
        }

        internal bool HasWrongCase
        {
            get { return Char.IsLetter(Expected) && Char.ToUpperInvariant(Expected) == Char.ToUpperInvariant(Entered); }
        }
    }

    internal sealed class SessionReport
    {
        private readonly Guid id = Guid.NewGuid();
        private readonly Course course;
        private readonly Lesson lesson;
        private readonly PracticeMode mode;
        private readonly DateTimeOffset started;
        private readonly DateTimeOffset finished;
        private readonly TimeSpan elapsed;
        private readonly TimeSpan limit;
        private readonly int typed;
        private readonly int processed;
        private readonly bool advanceOnError;
        private readonly InputActivity activity;
        private readonly string poemTitle;
        private readonly int accuracy;
        private readonly int speed;
        private readonly IGrouping<char, TypingMistake>[] byExpected;

        internal IReadOnlyList<TypingMistake> Errors { get; private set; }
        internal IReadOnlyList<string> Recommendations { get; private set; }
        internal int ErrorPositions { get; private set; }
        internal bool HasInput { get { return typed > 0 || Errors.Count > 0; } }
        internal string Summary
        {
            get
            {
                if (!HasInput) return "В этой попытке не было ввода.";
                if (Errors.Count == 0) return "Ошибок нет.";
                return String.Format("Ошибок {0} · позиций с ошибками {1}", Errors.Count, ErrorPositions);
            }
        }

        internal string FileName
        {
            get { return "Ритм_" + finished.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture) + "_" + course.Code + "_" + lesson.Id + ".md"; }
        }

        internal SessionReport(Exercise exercise, Course course, Lesson lesson)
        {
            if (exercise.Phase != Phase.Finished || exercise.IsDemo) throw new ArgumentException("A completed practice is required.", "exercise");
            this.course = course;
            this.lesson = lesson;
            mode = exercise.Mode;
            started = exercise.StartedAt;
            finished = exercise.FinishedAt;
            elapsed = exercise.Elapsed;
            limit = exercise.Limit;
            typed = exercise.Typed;
            processed = exercise.Processed;
            advanceOnError = exercise.AdvanceOnError;
            activity = exercise.Activity;
            poemTitle = exercise.Mode == PracticeMode.Lesson && exercise.Verse != null ? exercise.Verse.Title : null;
            accuracy = exercise.Accuracy;
            speed = exercise.Speed;
            Errors = Array.AsReadOnly(exercise.ErrorLog.ToArray());
            ErrorPositions = Errors.Select(error => error.GlobalPosition).Distinct().Count();
            byExpected = Errors.GroupBy(error => error.Expected).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).ToArray();
            Recommendations = Array.AsReadOnly(BuildRecommendations().ToArray());
        }

        private IEnumerable<string> BuildRecommendations()
        {
            if (!HasInput)
            {
                yield return "Начните новую попытку и наберите несколько слов, чтобы оценить точность.";
                yield break;
            }
            if (Errors.Count == 0)
            {
                yield return "Попробуйте чуть увеличить темп, сохраняя точность.";
                yield break;
            }

            int wrongLayout = Errors.Count(error => TypingMistake.IsWrongLayout(error.Entered, course.English));
            if (wrongLayout > 0)
                yield return "Перед стартом проверьте, что в Windows выбрана " + (course.English ? "английская" : "русская") + " раскладка. Ошибок с другой раскладкой — " + wrongLayout + ".";

            int wrongCase = Errors.Count(error => error.HasWrongCase);
            if (wrongCase > 0)
                yield return "При ошибке в регистре проверьте Caps Lock и нажимайте Shift рукой, противоположной руке набора.";

            var characters = Errors.Where(error => !error.HasWrongCase && !TypingMistake.IsWrongLayout(error.Entered, course.English))
                .GroupBy(error => error.Expected).OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
                .Take(3).Select(group => group.Key).ToArray();
            if (characters.Length > 0)
            {
                if (characters[0] == ' ')
                    yield return "Потренируйте пробел после каждого слова. Нажимайте его большим пальцем и сразу переходите к следующему слову.";
                else
                    yield return "Потренируйте " + String.Join(", ", characters.Select(character => "«" + TypingMistake.Label(character) + "»"))
                        + " в медленном темпе. Следите за подсказкой по пальцам.";
            }
        }

        internal void SaveMarkdown(string path)
        {
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), ".rhythm-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, ToMarkdown(), new UTF8Encoding(false));
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        internal string ToMarkdown()
        {
            string modeKey;
            string modeLabel;
            switch (mode)
            {
                case PracticeMode.Unlimited: modeKey = "unlimited"; modeLabel = "Без лимита"; break;
                case PracticeMode.FiveMinutes: modeKey = "five_minutes"; modeLabel = "5 минут"; break;
                case PracticeMode.TenMinutes: modeKey = "ten_minutes"; modeLabel = "10 минут"; break;
                default: modeKey = "lesson"; modeLabel = "Подборка"; break;
            }

            string layout = course.English ? "Английская QWERTY" : "Русская";
            string section = lesson == course.FreePractice ? "free_practice" : lesson.Section.ToString().ToLowerInvariant();
            var text = new StringBuilder();
            text.AppendLine("---").AppendLine("schema: rhythm.practice.v1");
            Field(text, "app_version", Quoted(App.Version));
            Field(text, "attempt_id", Quoted(id.ToString("D")));
            Field(text, "layout", Quoted(course.Code));
            Field(text, "lesson_id", Quoted(lesson.Id));
            Field(text, "lesson_title", Quoted(lesson.Title));
            Field(text, "section", Quoted(section));
            Field(text, "mode", Quoted(modeKey));
            Field(text, "activity", Quoted(activity.ToString().ToLowerInvariant()));
            if (poemTitle != null) Field(text, "poem_title", Quoted(poemTitle));
            Field(text, "error_policy", Quoted(advanceOnError ? "advance" : "retry"));
            Field(text, "started_at", Quoted(started.ToString("o", CultureInfo.InvariantCulture)));
            Field(text, "finished_at", Quoted(finished.ToString("o", CultureInfo.InvariantCulture)));
            Field(text, "elapsed_ms", (long)elapsed.TotalMilliseconds);
            Field(text, "limit_ms", limit > TimeSpan.Zero ? ((long)limit.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) : "null");
            Field(text, "correct_chars", typed);
            Field(text, "processed_chars", processed);
            Field(text, "errors", Errors.Count);
            Field(text, "error_positions", ErrorPositions);
            Field(text, "accuracy_percent", HasInput ? accuracy.ToString(CultureInfo.InvariantCulture) : "null");
            Field(text, "speed_cpm", HasInput ? speed.ToString(CultureInfo.InvariantCulture) : "null");
            text.AppendLine("---").AppendLine();
            text.AppendLine("# Ритм — отчёт о тренировке").AppendLine();
            text.Append("**").Append(Escape(lesson.Title)).Append("** · ").Append(layout).Append(" · ").AppendLine(modeLabel).AppendLine();
            text.AppendLine("| Показатель | Результат |").AppendLine("| --- | ---: |");
            Row(text, "Завершено", finished.ToString("dd.MM.yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture));
            Row(text, "Время набора", String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds));
            Row(text, "Правильно набрано знаков", typed);
            if (activity != InputActivity.Typing)
                Row(text, "Задание", activity == InputActivity.Dictation ? "Набор на слух" : "Набор с текстом и озвучкой");
            if (poemTitle != null) Row(text, "Произведение", poemTitle);
            Row(text, "После ошибки", advanceOnError ? "Переход к следующему знаку" : "Повтор знака");
            Row(text, "Скорость, зн/мин", HasInput ? speed.ToString(CultureInfo.InvariantCulture) : "—");
            Row(text, "Точность", HasInput ? accuracy.ToString(CultureInfo.InvariantCulture) + "%" : "—");
            Row(text, "Ошибок", Errors.Count);
            Row(text, "Позиций с ошибками", ErrorPositions);
            text.AppendLine().AppendLine("## Рекомендации").AppendLine();
            foreach (string recommendation in Recommendations) text.Append("- ").AppendLine(Escape(recommendation));

            if (Errors.Count == 0)
            {
                text.AppendLine().AppendLine(Summary);
                return text.ToString();
            }

            text.AppendLine().AppendLine("## Ошибки по ожидаемым знакам").AppendLine();
            text.AppendLine("| Знак | Код Unicode | Ошибок |").AppendLine("| --- | --- | ---: |");
            foreach (var group in byExpected) Row(text, TypingMistake.Label(group.Key), TypingMistake.CodePoint(group.Key), group.Count());
            text.AppendLine().AppendLine("## Где были ошибки").AppendLine();
            text.AppendLine("Нумерация начинается с 1. Ожидаемый знак в контексте выделен скобками, `␣` обозначает пробел.").AppendLine();
            text.AppendLine("| № | Фрагмент | Знак во фрагменте | Знак с начала | Ожидалось | Введено | Код ожидаемого | Код введённого | Контекст | Время от старта, мс |");
            text.AppendLine("| ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- | ---: |");
            foreach (TypingMistake error in Errors)
                Row(text, error.Number, error.Page, error.Position, error.GlobalPosition, error.ExpectedLabel, error.EnteredLabel,
                    TypingMistake.CodePoint(error.Expected), TypingMistake.CodePoint(error.Entered), error.Context, error.ElapsedMilliseconds);
            return text.ToString();
        }

        private static void Field(StringBuilder text, string name, object value)
        {
            text.Append(name).Append(": ").AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static string Quoted(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private static void Row(StringBuilder text, params object[] values)
        {
            text.Append("| ");
            foreach (object value in values) text.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture))).Append(" | ");
            text.AppendLine();
        }

        private static string Escape(string value)
        {
            var escaped = new StringBuilder();
            foreach (char character in value)
            {
                if ("\\`*_{}[]<>&|~".IndexOf(character) >= 0) escaped.Append("&#").Append((int)character).Append(';');
                else escaped.Append(character);
            }
            return escaped.ToString();
        }
    }
}
