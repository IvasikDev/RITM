using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RhythmTyping
{
    internal sealed class PracticeCorpus
    {
        private readonly bool english;
        private readonly string[] words;
        private readonly string[] names;
        private readonly string[] actions;
        private readonly string[] sentences;
        private readonly string[] dialogue;
        private readonly string[] stories;
        private readonly string[] punctuation;

        internal PracticeCorpus(bool english)
        {
            this.english = english;
            words = (english ? CorpusText.EnglishWords : CorpusText.RussianWords)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            names = Parts(english ? CorpusText.EnglishNames : CorpusText.RussianNames);
            actions = Parts(english ? CorpusText.EnglishActions : CorpusText.RussianActions);
            sentences = Pair(names, actions, (name, action) => name + " " + action + ".")
                .Concat(Pair(Parts(english ? CorpusText.EnglishTimes : CorpusText.RussianTimes),
                    Parts(english ? CorpusText.EnglishEvents : CorpusText.RussianEvents), (time, action) => time + " " + action + ".")).ToArray();
            dialogue = Scenes(english ? CorpusText.EnglishDialogue : CorpusText.RussianDialogue).ToArray();
            stories = Scenes(english ? CorpusText.EnglishStories : CorpusText.RussianStories).ToArray();
            punctuation = Scenes(english ? CorpusText.EnglishPunctuation : CorpusText.RussianPunctuation).ToArray();
        }

        internal IEnumerable<string> ForLesson(Lesson lesson)
        {
            switch (lesson.Section)
            {
                case LessonSection.Letters:
                    if (lesson.Id == (english ? "15" : "17")) return Pair(names, actions, (name, action) => name + " " + action);
                    return KeyDrills(lesson);
                case LessonSection.Words:
                    if (lesson.Id == "20") return words.Where(word => word.Length >= 2 && word.Length <= 4);
                    if (lesson.Id == "21") return words.Where(word => word.Length >= 8);
                    if (english && lesson.Id == "14") return WordsWithClusters();
                    return words.Where(word => word.Length >= 5 && word.Length <= 10);
                case LessonSection.Symbols:
                    if (lesson.Id == "22") return NumberAndSymbolDrills();
                    if (lesson.Id == (english ? "16" : "18")) return NumberSentences();
                    return punctuation.Concat(dialogue);
                default:
                    return lesson.Id == (english ? "18" : "19") ? dialogue : stories;
            }
        }

        internal IEnumerable<string> FreePractice()
        {
            return sentences.Concat(stories).Concat(dialogue).Concat(punctuation).Concat(NumberSentences());
        }

        private IEnumerable<string> KeyDrills(Lesson lesson)
        {
            var allowed = new HashSet<char>(lesson.Units.SelectMany(unit => unit).Where(c => c != ' '));
            char[] keys = allowed.OrderBy(c => c).ToArray();
            char[] focus = lesson.Keys.ToLowerInvariant().Where(allowed.Contains).Distinct().ToArray();
            var targets = new HashSet<char>(focus);
            string[] matchingWords = words.Where(word => word.Length >= 2 && word.All(allowed.Contains) && word.Any(targets.Contains)).ToArray();
            foreach (string word in matchingWords) yield return word;
            int desired = Math.Max(28, 116 - matchingWords.Length);
            var seen = new HashSet<string>(lesson.Units.Concat(matchingWords), StringComparer.Ordinal);
            Random random = new Random(2099 + Int32.Parse(lesson.Id, CultureInfo.InvariantCulture) + (english ? 1619 : 0));
            for (int attempt = 0; desired > 0 && attempt < 2400; attempt++)
            {
                char[] drill = new char[random.Next(3, 8)];
                int repeat = 0;
                for (int index = 0; index < drill.Length; index++)
                {
                    char[] alphabet = index % 2 == 0 ? focus : keys;
                    drill[index] = alphabet[random.Next(alphabet.Length)];
                    repeat = index > 0 && drill[index] == drill[index - 1] ? repeat + 1 : 1;
                    if (repeat == 4) break;
                }
                if (repeat == 4) continue;
                string unit = new string(drill);
                if (!seen.Add(unit)) continue;
                desired--;
                yield return unit;
            }
        }

        private IEnumerable<string> WordsWithClusters()
        {
            string[] clusters = { "th", "sh", "ch", "wh", "qu" };
            return words.Where(word => clusters.Any(word.Contains))
                .Concat(CorpusText.EnglishClusters.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private IEnumerable<string> NumberSentences()
        {
            for (int index = 0; index < 112; index++)
            {
                int route = 12 + index;
                int house = 5 + index * 3;
                int hour = 6 + index % 16;
                int minute = index * 7 % 60;
                string time = Format("{0:00}:{1:00}", hour, minute);
                switch (index % 4)
                {
                    case 0: yield return english ? Format("Bus {0} leaves at {1}.", route, time) : Format("Автобус {0} отправляется в {1}.", route, time); break;
                    case 1: yield return english ? Format("Meet at {0} outside number {1}.", time, house) : Format("Встречаемся в {0} у дома {1}.", time, house); break;
                    case 2: yield return english ? Format("Room {0}, desk {1}.", house, route % 13 + 1) : Format("Кабинет {0}, стол {1}.", house, route % 13 + 1); break;
                    default: yield return english ? Format("Order {0} is ready after {1}.", house + 340, time) : Format("Заказ {0} будет готов после {1}.", house + 340, time); break;
                }
            }
        }

        private IEnumerable<string> NumberAndSymbolDrills()
        {
            for (int index = 0; index < 108; index++)
            {
                int a = 3 + index % 42;
                int b = 1 + index / 4;
                switch (index % 6)
                {
                    case 0: yield return Format("{0}+{1}={2}", a, b, a + b); break;
                    case 1: yield return Format("{0}-{1}={2}", a + b, b, a); break;
                    case 2: yield return Format("({0}+{1})*{2}", a, b, index % 4 + 2); break;
                    case 3: yield return english ? Format("${0}.{1:00} + ${2}.00", a, index, b) : Format("{0},{1:00} + {2},00", a, index, b); break;
                    case 4: yield return english ? Format("[{0}] #{1}", a, b) : Format("({0}) №{1}", a, b); break;
                    default: yield return Format("{0}% - {1}%", 100 - index / 2, b / 2); break;
                }
            }
        }

        private static string Format(string format, params object[] values)
        {
            return String.Format(CultureInfo.InvariantCulture, format, values);
        }

        private static string[] Parts(string phraseSet)
        {
            return phraseSet.Split('|');
        }

        private static IEnumerable<string> Scenes(string[][] scenes)
        {
            foreach (string[] scene in scenes)
            {
                foreach (string pair in Pair(Parts(scene[0]), Parts(scene[1]), (first, second) => first + " " + second))
                    yield return pair;
            }
        }

        private static IEnumerable<string> Pair(string[] first, string[] second, Func<string, string, string> combine)
        {
            foreach (string left in first)
                foreach (string right in second)
                    yield return combine(left, right);
        }
    }
}
