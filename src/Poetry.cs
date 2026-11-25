using System;
using System.Collections.Generic;
using System.Linq;

namespace RhythmTyping
{
    internal sealed class Poem
    {
        internal readonly string Title;
        internal readonly string[] Lines;

        internal Poem(string title, string text)
        {
            Title = title;
            Lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToArray();
        }
    }

    internal sealed class VersePage
    {
        internal readonly string Title;
        internal readonly string[] Lines;

        internal VersePage(string title, IEnumerable<string> lines)
        {
            Title = title;
            Lines = lines.ToArray();
        }
    }

    internal sealed class VerseDeck
    {
        private readonly VersePage[][] poems;
        private int poem;
        private int page;

        internal int FirstPoemPages { get { return poems[0].Length; } }

        internal VerseDeck(Poem[] samples, Random random)
        {
            poems = samples.OrderBy(_ => random.Next()).Select(Paginate).ToArray();
        }

        internal VersePage NextPage()
        {
            VersePage next = poems[poem][page++];
            if (page == poems[poem].Length)
            {
                page = 0;
                poem = (poem + 1) % poems.Length;
            }
            return next;
        }

        private static VersePage[] Paginate(Poem poem)
        {
            var pages = new List<VersePage>();
            var lines = new List<string>();
            int length = 0;
            foreach (string line in poem.Lines)
            {
                if (lines.Count > 0 && (lines.Count == 4 || length + 1 + line.Length > PassageGenerator.MaxUnitLength))
                {
                    pages.Add(new VersePage(poem.Title, lines));
                    lines.Clear();
                    length = 0;
                }
                length += (lines.Count == 0 ? 0 : 1) + line.Length;
                lines.Add(line);
            }
            pages.Add(new VersePage(poem.Title, lines));
            return pages.ToArray();
        }
    }

    // Public-domain originals. Dashes, ellipses and apostrophes use keyboard equivalents.
    internal static class Poetry
    {
        internal static readonly Poem[] Pushkin =
        {
            new Poem("Я вас любил...", @"Я вас любил: любовь ещё, быть может,
В душе моей угасла не совсем;
Но пусть она вас больше не тревожит;
Я не хочу печалить вас ничем.
Я вас любил безмолвно, безнадежно,
То робостью, то ревностью томим;
Я вас любил так искренно, так нежно,
Как дай вам бог любимой быть другим."),
            new Poem("Туча", @"Последняя туча рассеянной бури!
Одна ты несёшься по ясной лазури,
Одна ты наводишь унылую тень,
Одна ты печалишь ликующий день.
Ты небо недавно кругом облегала,
И молния грозно тебя обвивала;
И ты издавала таинственный гром
И алчную землю поила дождём.
Довольно, сокройся! Пора миновалась,
Земля освежилась, и буря промчалась,
И ветер, лаская листочки древес,
Тебя с успокоенных гонит небес."),
            new Poem("Я помню чудное мгновенье...", @"Я помню чудное мгновенье:
Передо мной явилась ты,
Как мимолётное виденье,
Как гений чистой красоты.
В томленьях грусти безнадежной,
В тревогах шумной суеты,
Звучал мне долго голос нежный
И снились милые черты.
Шли годы. Бурь порыв мятежный
Рассеял прежние мечты,
И я забыл твой голос нежный,
Твои небесные черты.
В глуши, во мраке заточенья
Тянулись тихо дни мои
Без божества, без вдохновенья,
Без слёз, без жизни, без любви.
Душе настало пробужденье:
И вот опять явилась ты,
Как мимолётное виденье,
Как гений чистой красоты.
И сердце бьётся в упоенье,
И для него воскресли вновь
И божество, и вдохновенье,
И жизнь, и слёзы, и любовь.")
        };

        internal static readonly Poem[] Lermontov =
        {
            new Poem("Парус", @"Белеет парус одинокий
В тумане моря голубом!..
Что ищет он в стране далёкой?
Что кинул он в краю родном?..
Играют волны - ветер свищет,
И мачта гнётся и скрыпит...
Увы! он счастия не ищет
И не от счастия бежит!
Под ним струя светлей лазури,
Над ним луч солнца золотой...
А он, мятежный, просит бури,
Как будто в бурях есть покой!"),
            new Poem("Утёс", @"Ночевала тучка золотая
На груди утёса-великана;
Утром в путь она умчалась рано,
По лазури весело играя;
Но остался влажный след в морщине
Старого утёса. Одиноко
Он стоит, задумался глубоко,
И тихонько плачет он в пустыне."),
            new Poem("На севере диком...", @"На севере диком стоит одиноко
На голой вершине сосна,
И дремлет качаясь, и снегом сыпучим
Одета, как ризой, она.
И снится ей всё, что в пустыне далёкой -
В том крае, где солнца восход,
Одна и грустна на утёсе горючем
Прекрасная пальма растёт.")
        };

        internal static readonly Poem[] Shakespeare =
        {
            new Poem("Sonnet 18", @"Shall I compare thee to a summer's day?
Thou art more lovely and more temperate:
Rough winds do shake the darling buds of May,
And summer's lease hath all too short a date:
Sometime too hot the eye of heaven shines,
And often is his gold complexion dimm'd;
And every fair from fair sometime declines,
By chance, or nature's changing course, untrimm'd;
But thy eternal summer shall not fade,
Nor lose possession of that fair thou ow'st;
Nor shall death brag thou wander'st in his shade,
When in eternal lines to time thou grow'st;
So long as men can breathe, or eyes can see,
So long lives this, and this gives life to thee."),
            new Poem("Sonnet 29", @"When, in disgrace with fortune and men's eyes,
I all alone beweep my outcast state,
And trouble deaf heaven with my bootless cries,
And look upon myself, and curse my fate,
Wishing me like to one more rich in hope,
Featured like him, like him with friends possess'd,
Desiring this man's art and that man's scope,
With what I most enjoy contented least;
Yet in these thoughts myself almost despising,
Haply I think on thee, and then my state,
Like to the lark at break of day arising
From sullen earth, sings hymns at heaven's gate;
For thy sweet love remember'd such wealth brings
That then I scorn to change my state with kings."),
            new Poem("Sonnet 73", @"That time of year thou mayst in me behold
When yellow leaves, or none, or few, do hang
Upon those boughs which shake against the cold,
Bare ruin'd choirs, where late the sweet birds sang.
In me thou see'st the twilight of such day
As after sunset fadeth in the west,
Which by and by black night doth take away,
Death's second self, that seals up all in rest.
In me thou see'st the glowing of such fire
That on the ashes of his youth doth lie,
As the death-bed whereon it must expire,
Consumed with that which it was nourish'd by.
This thou perceiv'st, which makes thy love more strong,
To love that well which thou must leave ere long."),
            new Poem("Sonnet 130", @"My mistress' eyes are nothing like the sun;
Coral is far more red than her lips' red;
If snow be white, why then her breasts are dun;
If hairs be wires, black wires grow on her head.
I have seen roses damask'd, red and white,
But no such roses see I in her cheeks;
And in some perfumes is there more delight
Than in the breath that from my mistress reeks.
I love to hear her speak, yet well I know
That music hath a far more pleasing sound;
I grant I never saw a goddess go;
My mistress, when she walks, treads on the ground:
And yet, by heaven, I think my love as rare
As any she belied with false compare.")
        };
    }
}
