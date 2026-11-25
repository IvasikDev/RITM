using System;
using System.Collections.Generic;
using System.Linq;

namespace RhythmTyping
{
    internal enum LessonSection { Letters, Words, Symbols, Texts }

    internal sealed class Lesson
    {
        internal readonly string Id;
        internal readonly string Title;
        internal readonly string Keys;
        internal readonly string Instruction;
        internal readonly string[] Units;
        internal readonly LessonSection Section;
        internal readonly Poem[] Poems;
        internal bool IsPoetry { get { return Poems != null; } }

        internal Lesson(string id, string title, string keys, string instruction, string text, LessonSection section = LessonSection.Letters)
            : this(id, title, keys, instruction, text.Split(text.IndexOf('|') >= 0 ? '|' : ' '), section)
        {
        }

        private Lesson(string id, string title, string keys, string instruction, string[] units, LessonSection section, Poem[] poems = null)
        {
            Id = id;
            Title = title;
            Keys = keys;
            Instruction = instruction;
            Units = units;
            Section = section;
            Poems = poems;
        }

        internal Lesson(string id, string title, Poem[] poems)
            : this(id, title, "Стихи", "Набирайте строки по порядку. В конце строки нажимайте пробел, когда он подсвечен.",
                poems.SelectMany(poem => poem.Lines).ToArray(), LessonSection.Texts, poems)
        {
        }

        internal Lesson WithMaterial(IEnumerable<string> additional)
        {
            return new Lesson(Id, Title, Keys, Instruction, Units.Concat(additional)
                .Select(unit => unit.Trim()).Where(unit => unit.Length > 0 && unit.Length <= PassageGenerator.MaxUnitLength)
                .Distinct(StringComparer.Ordinal).ToArray(), Section);
        }
    }

    internal sealed class Course
    {
        internal readonly bool English;
        internal readonly string Code;
        internal readonly Lesson[] Lessons;
        internal readonly Lesson FreePractice;

        internal Course(bool english, Lesson[] lessons, string freeText)
        {
            English = english;
            Code = english ? "en" : "ru";
            var corpus = new PracticeCorpus(english);
            Lessons = lessons.Select(lesson => lesson.IsPoetry ? lesson : lesson.WithMaterial(corpus.ForLesson(lesson))).ToArray();
            FreePractice = new Lesson("free", "Свободная практика", "",
                english ? "Набирайте английский текст в своём темпе." : "Набирайте русский текст в своём темпе.", freeText)
                .WithMaterial(corpus.FreePractice());
        }
    }

    internal static class Catalog
    {
        private static Lesson L(int id, string title, string keys, string instruction, string text, LessonSection section = LessonSection.Letters)
        {
            return new Lesson(id.ToString(System.Globalization.CultureInfo.InvariantCulture), title, keys, instruction, text, section);
        }

        internal static readonly Course Russian = new Course(false, new[]
        {
            L(0, "Первые клавиши", "А О В Л", "Поставьте указательные пальцы на А и О, средние — на В и Л.",
                "ао оа аа оо ва ло ав ол во ла ов ал ава ово вав лол вов лал аол ова аов лова вала лава овала вал овал лав вол вола лов алло овалов"),
            L(1, "Средний ряд", "Ф Ы Д Ж Э", "Держите указательные на А и О. На Ф, Ж и Э тянитесь мизинцами.",
                "фы дж жэ дэ жа до лы да ад фа фо аф эфф эжа жда два дало дала долы лады валы волы лава вол ода вода вывод вдова лыжа жало лад выл выдал фалда фал ждал"),
            L(8, "Центр среднего ряда", "П Р", "Левый указательный нажимает П, правый указательный — Р.",
                "ап ро па ор пр рп ра по пар вор дар пора пара права провал рвал жар двор фара дрова правда повар лапа папа радар прорыв прав валов оправа повара"),
            L(9, "Вверх от центра", "К Е Н Г", "К и Е нажимайте левым указательным, Н и Г — правым.",
                "ке нг ак ан го ге кен гон река нора парк горка нога дорога корова колонна окно ведро ограда код крен вагон рано рана пена перо вера кран гранка гонка полено анклав голова гора плен гренка"),
            L(2, "Указательные", "А П К Е · О Р Н Г", "Возвращайте указательные пальцы на выступы А и О.",
                "апа оро ака оно аеа ого апек орнг капкан канава неон огонек река прогон ворон ворона кран гравер акр аккорд радар пора конка рога кора канон арка грек рог рок перрон клад кладовка"),
            L(10, "Верхний ряд слева", "Й Ц У", "Й нажимайте мизинцем, Ц — безымянным, У — средним пальцем левой руки.",
                "йц цй цу уц йа ау уй ца цо ура рука урок угол угар луг круг цок цокал урока уйду уеду угадал рукав цаца курган курок кукла укол дойка край оконце гудок"),
            L(11, "Верхний ряд справа", "Ш Щ З", "Ш нажимайте средним, Щ — безымянным, З — мизинцем правой руки.",
                "шщ щш зш шз шла ща шу зу шар щука щуп щупал лужа шкура шкаф шов шнур окно звезда звон звонок загар узор зазор защелка урон заезд шор шпала шепнула шуровал"),
            L(12, "Край верхнего ряда", "Х Ъ", "Обе клавиши нажимайте правым мизинцем.",
                "ха ох хъ ъх хе ух эх ах хво ухо хохол выход вход холод ход ходок хандра хор хорек верх вхожу подъезд разъезд разъехал въезд въехал подъехал уехал охрана хвала холодок"),
            L(3, "Весь верхний ряд", "Й Ц У К Е Н Г Ш Щ З Х Ъ", "После движения вверх возвращайте пальцы на средний ряд.",
                "йц уц шщ зх хъ край узор щука шелуха заезд ухо угол подъезд шорох канал кран цех укол щуп звезда шнур кожух голова народ волна довод провод выход въезд разъезд ехал рукав хлев радуга урожай"),
            L(13, "Нижний ряд слева", "Я Ч С", "Я нажимайте мизинцем, Ч — безымянным, С — средним пальцем левой руки.",
                "яч чс ся яя оч ач са ячейка ясная роса часы час часок удача дача случай сахар сено окно саранча сарай ясна ясный двор сад садовод след слово слог срок скала оса сало серый чужак чарка чашка"),
            L(14, "Центр нижнего ряда", "М И Т", "М и И — левый указательный. Т — правый указательный.",
                "ми им ит ти ат та мир миг мята нота нитка ниточка меч метка комната команда матча кит касса каток мимо тихо тихая стая стадо сито мотор метр ритм монтаж мох мода туман трава трамвай митинг"),
            L(15, "Нижний ряд справа", "Ь Б Ю", "Ь — правый указательный, Б — средний, Ю — безымянный.",
                "ьб бю ью юб аб бо соль тюль быть брать июнь июль любовь блюдо рубль бусы бухта бант борт боль тополь больше сентябрь голубь мебель альбом барабан брюки верблюд юбка юла юный баян баянчик"),
            L(4, "Весь нижний ряд", "Я Ч С М И Т Ь Б Ю", "Тянитесь вниз нужным пальцем. После нажатия возвращайте его на средний ряд.",
                "мяч часть мост нить бусы люди лимон семья письмо тюльпан сумка таблица тишина читатель стих работа робот маяк любитель мальчик библиотека чайник синица лист юбка малина курс темп ткань честность месяц сердце улица ключ печать компьютер"),
            L(16, "Ё, Ъ и Ь", "Ё Ъ Ь", "Ё нажимайте левым мизинцем. Для Ъ и Ь работают разные пальцы правой руки.",
                "ёж ёлка ёмкость ёрш ёжик мёд лён лёд полёт звёзды слёзы стёкла тётя берёза учёба свёкла пчёлы объём объезд подъезд съёмка съел объявить разъём въезд подъём письмо семья кольцо конь пальто фасоль метель вьюга отъезд ружьё"),
            L(17, "Заглавные буквы", "Shift · А–Я", "Нажимайте Shift рукой, противоположной той, которой набираете букву.",
                "Анна и Павел|Оля звонит Лене|Миша и Катя дома|Саша пишет Вере|Дима читает Нине|Ирина встретила Олега|Омск и Тула|Поезд в Казань|На мосту в Перми|Встреча в Томске|Из Москвы в Рязань|Над Волгой туман|Город Сочи у моря|В Сибири холодно|Урал и Алтай|Выходные в Пскове|Книга для Юры|Ёлка для Жени|У Марии новый зонт"),
            L(20, "Короткие слова", "2–4 буквы", "Набирайте короткие слова целиком, сохраняя равномерные пробелы.",
                "мы вы он ты бы уж за из на по от до но да не ни ли как где кто что все там тут вот два три дом лес сад луг мир лёд мёд мак шар мел дым сок рис сыр чай ёж их дуб пень снег ключ лист мост кран шёл шов зонт рука река день лето зима поле", LessonSection.Words),
            L(5, "Частые слова", "А–Я · Ё", "Пробел нажимайте большим пальцем и держите ровный темп.",
                "время люди работа слово место дело жизнь город утро солнце вечер школа друзья книга потом сегодня завтра всегда просто вопрос ответ немного жёлтый тёплый тихий окно рядом белый совсем можно река дорога воздух весна зима лето осень дом день новый хороший быстро вокруг искать думать делать читать писать смотреть идти стоять держать рассказ пример знание рука клавиша пальцы ключ скорость точность", LessonSection.Words),
            L(21, "Длинные слова", "8 букв и больше", "Читайте слово вперёд и двигайте пальцами в ровном темпе до его конца.",
                "клавиатура путешествие библиотека воображение внимательность расписание тренировка аквариум велосипед земляника воскресенье набережная пространство конфигурация одноклассник наблюдение направление воспоминание инструменты расстояние фотография упражнение спутниковый пробуждение деревенский предложение многоточие водохранилище трамвайный растениевод светоотражатель", LessonSection.Words),
            L(18, "Цифры и время", "0–9", "Набирайте цифры в верхнем ряду клавиатуры, двигая только нужный палец.",
                "0123456789|9876543210|12 34 56 78 90|Встреча в 09:15|Поезд в 18:42|Урок номер 7|Дом 35, квартира 62|Открываемся в 08:00|В книге 256 страниц|Маршрут 124|Через 45 минут|До финиша 600 метров|Код 740813|Цена 1490 рублей|2 чашки и 3 ложки|Зал 8, ряд 6|Дата 24.11.2026|Счёт 3:2|Смены в 06:55 и 16:20|Комната 504", LessonSection.Symbols),
            L(22, "Клавиши со знаками", "( ) · + - · % №", "Повторяйте группы знаков. Для верхнего символа удерживайте Shift противоположной рукой.",
                "! ? . ,|: ; , .|( ) ( )|+ - =|% № %|* * +|!? ?!|() ()|+- =+|-_ _-|,, ..|:; ;:|№% %№|1+2=3|5-3=2|50% 25%|№7 №14|(2+8)*3|3,5 0,6|10:45 08:15|3:2 2:3|03.05 06.08|+7 -5", LessonSection.Symbols),
            L(6, "Знаки и Shift", ".,!?;:", "Для клавиш со знаком в углу удерживайте Shift противоположной рукой.",
                "Слышишь, как шумит дождь?|Смотри, выглянуло солнце!|Он уже дома.|Ключ, зонт, билет.|Здесь тихо; можно читать.|Приходи в 10:30.|Какой сегодня день?|Встреча не отменяется!|Проверь всё: свет, воду, дверь.|Я уже близко, ты где?|Нет, это соседняя улица.|Хлеб свежий; чай горячий.|Дорога длинная, но светлая.|На полке 3 книги и 2 тетради.|Вот оно, письмо!|Точка, запятая, вопросительный знак.", LessonSection.Symbols),
            L(19, "Реплики и переписка", "А–Я · ? !", "Держите ритм на коротких словах и проверяйте знаки в конце реплик.",
                "Привет! Ты уже в пути?|Да, я буду через десять минут.|Запиши мой адрес: дом 18, подъезд 2.|Отправь мне новый маршрут, пожалуйста.|Понял, встречаемся у входа.|Ты успеешь к семи?|Я куплю воду, хлеб и фрукты.|Добрый день, встреча состоится?|Да, будем ждать вас в 14:00.|Я оставил записку на столе.|Хорошо, прочитаю её вечером.|Куда пропал мой блокнот?|Посмотри на верхней полке.|Спасибо, нашёл!|Договорились, увидимся завтра.", LessonSection.Texts),
            L(7, "Связный текст", "А–Я · 0–9 · ЗНАКИ", "Переводите взгляд на несколько букв вперёд и сохраняйте ровный темп.",
                "В 7 утра мы вышли к реке. На тропе блестела роса, а над водой стелился туман.|У окна тихо тикали старые часы. Я раскрыл книгу и начал читать.|Я свернул карту и пошёл вдоль берега. Вдали был виден мост.|Сосед открыл калитку и позвал собаку. Она тут же выбежала на дорожку.|В комнате пахло свежим хлебом. Я налил себе чаю и сел у окна.|Когда ветер утих, над лесом поднялась луна. Мы погасили фонари.|Поезд пришёл вовремя. Мы успели на встречу и даже зашли за чаем.|На обратной дороге мы свернули к озеру. Там уже зажглись фонари.|Листья укрыли дорожку плотным ковром. В осеннем саду щебетали птицы.|После дождя за домом появилась радуга. Я позвал детей к окну.|Утром город просыпается за несколько минут. Сначала во двор выезжает хлебный фургон.|За мостом стоял небольшой дом. В окне горел тёплый жёлтый свет.|Я потерял ключи в саду. Через 10 минут их нашёл сосед.|Она достала из шкафа старый альбом. Между страницами лежал сухой лист.", LessonSection.Texts),
            new Lesson("23", "Стихи Пушкина", Poetry.Pushkin),
            new Lesson("24", "Стихи Лермонтова", Poetry.Lermontov)
        }, "Зимой ёлки покрыты снегом, а весной у дома цветёт сирень.|Летом над садом летают птицы, и в тени приятно сидеть.|Вечерний трамвай свернул за угол и остановился у парка.|По дороге к морю мы зашли в маленькую пекарню.|На столе лежали две карты, компас и записная книжка.|Где начинается тропа к старой башне?|Через 15 минут над полями взошло солнце.|Анна оставила у двери пакет с яблоками.|Я перечитал письмо и положил его возле окна.|Сначала набери фразу спокойно, потом добавь скорость.|В 09:40 библиотека уже была открыта.|На воде мерцали отражения фонарей.|За поворотом дорога стала узкой и тихой.|В комнате уютно пахло деревом и мятой.|К полудню облака рассеялись, и показались горы.|Скоро станция, не забудь зонт!|Миша потянулся за чашкой и открыл блокнот.|Дом 46 находится за школой, рядом с аптекой.|Утром дворник расчистил дорожку от снега.|Собака вздрогнула, но осталась лежать у двери.|Мы нашли укромную поляну и устроили там привал.|Белка спрятала орех и быстро взобралась на сосну.|Позвони мне после 18:00, когда вернёшься домой.|Весь день за окном шёл дождь, а к вечеру небо прояснилось.|Какой маршрут короче: через рынок или по набережной?|Через три дома начинался бульвар с липами.|На обложке книги кто-то нарисовал маленькую лодку.|В коридоре был слышен тихий гул голосов.|На стене висела фотография знакомой улицы.|Мы сели у окна, чтобы увидеть первые огни города.");

        internal static readonly Course English = new Course(true, new[]
        {
            L(0, "Первые клавиши", "F J", "Поставьте указательные на клавиши F и J с выступами.",
                "fj jf ff jj fjf jfj ffj fjj jff jjf fjjj jfff fffj jjjf fjfj jfjf ffjj jjff fjfjf jfjfj ffjff jjfjj fjjfj jffjf ffffj jjjjf fjffj jfjjf"),
            L(1, "Средние пальцы", "D K · F J", "D нажимайте левым средним, K — правым средним.",
                "dk kd dd kk dfd kjk df jk dfjk kjfd dkfj jfdk dkk kdd fk jd dj kf dkj kdf kfjd djfk fdk jkd fddf jkkj kdkf djdk jfdj"),
            L(2, "Безымянные пальцы", "S L · D K F J", "S нажимайте левым безымянным, L — правым безымянным.",
                "sl ls ss ll sdf jkl sdk ljf sdl kls dsl jls fld kld lsd fdsl jkls dklf ldks skjl lfds sdfk lkjd sklf ldsj flsd lsdk skfs lslj"),
            L(3, "Мизинцы", "A ;", "A нажимайте левым мизинцем, точку с запятой — правым.",
                "a; ;a aa ;; as sa al la ask all fall add dad sad lad salad flask flak alas alaska salsa fall; ask; add; dallas lad; sad; salad; dads lads falls asks flasks salads"),
            L(4, "Центр среднего ряда", "G H", "Левый указательный нажимает G, правый указательный — H.",
                "fg jh gh hg ag ah gag hah gas hag glad hall half lash dash flag glass shall flash gala gall ash hash gash sag lag hags shag glass; glad; flags halls flash; half; hash; falls"),
            L(5, "Весь средний ряд", "A S D F G H J K L ;", "После каждого движения возвращайте пальцы в исходное положение.",
                "a sad lad|a glad dad|a glass flask|ask dad|ask a lad|a dash; a flash|a flag; a hall|salad; salsa|a gas flask|a flash; a flag|fall; ask; add|a half glass|a sad gal|a glad lass|a glass hall|a flag falls|dad asks|a lad adds|a glad lad|a gas flash"),
            L(6, "Верхний ряд слева", "Q W E", "Q, W и E набирают мизинец, безымянный и средний пальцы левой руки.",
                "qw we eq qwq ewe wed was sew sea ease lease flash wheel shell shelf shed weed deal lead seed seal heed head wade swell well week feel feed fees weeds saw qwe ale ales eels she sells seashells"),
            L(7, "Центр верхнего ряда", "R T Y U", "Для R и T используйте левый указательный, для Y и U — правый.",
                "rt yu ry tu try true tray tea tear trust great treat start street sure guest rest rush hurt dust study steady ready early later water alert yell day dry yard year data draft rust stray trays sturdy quest equal quarter"),
            L(8, "Верхний ряд справа", "I O P", "I — средний, O — безымянный, P — мизинец правой руки.",
                "io op pi oil poi pilot post port road rail paid pair paper quiet radio route tower power powerful quality airport area ratio repeat ripple rope hope shop ship short sharp slight though weight upper lower row rower issue"),
            L(9, "Весь верхний ряд", "Q W E R T Y U I O P", "Следите, чтобы ладонь не тянулась за пальцами к верхнему ряду.",
                "quiet hour|a paper route|water the tree|write a short tale|the road is steep|hold the rope|the sky is pale|we saw a white gull|the weather is dry|a useful tool|wait at the gate|sort the data|a short reply|this rail route|our little pilot|there is a leak|a layer of glass|a quiet yard|people walk to the west|a wide hallway"),
            L(10, "Нижний ряд слева", "Z X C", "Z, X и C набирают мизинец, безымянный и средний пальцы левой руки.",
                "zx xc cz exact taxi text extra exit excuse exercise cat catch cake check chart trace scare close choice clear city cycle cereal space race face ice price pizza gaze zoo size lazy craze crazy puzzle dizzy quiz cozy cedar card spice score"),
            L(11, "Центр нижнего ряда", "V B N M", "V и B нажимайте левым указательным, N и M — правым.",
                "vb nm van move name mean mint mind mobile family volume video vivid vine visit valley brave bridge brain burn blend bring brown blank blue amber lemon moment month mountain motion number normal answer open never morning room mouse team time dream small travel window"),
            L(12, "Весь нижний ряд", "Z X C V B N M", "Пробел нажимайте большим пальцем, сохраняя свободное положение кисти.",
                "a blue bird|a small box|a bright moon|move the table|bring a velvet bag|the next station|a calm voice|a brown fox|a green canvas|mix the paint|six black pens|a velvet ribbon|sit by the window|a clean blanket|a long bridge|a silver coin|a heavy book|a narrow lane|visit the old town|a vivid sunset|a box of lemons"),
            L(15, "Заглавные буквы", "Shift · A–Z", "Удерживайте Shift рукой, противоположной той, которой набираете букву.",
                "Anna and Ben|Clara walks with David|Emma writes to Fred|Grace visits Helen|Ivan calls Julia|Kate and Liam|Mia meets Noah|Olivia reads to Paul|Quinn calls Ruth|Sam talks to Tara|Uma joins Victor|Wendy meets Xavier|Yara calls Zoe|From London to York|A trip to Lisbon|On Monday in June|Jack lives near Lake Erie|Friday at Brighton Pier|Boston and Seattle"),
            L(20, "Короткие слова", "2–4 буквы", "Печатайте короткие слова в ровном темпе, включая пробелы между ними.",
                "am as at be by do go he if in is it me my no of on or so to up us we an and any are bad bag bed big bus can car cat cup day dog ear end far few fox fun get had hat hot ice job key law lip log map mix nap new oak one out pen pet pie red run saw sea sit sky son sun tea ten the top try two use van wax web wet win yes zoo ball bird boat book calm coat desk door fish frog hand home lake leaf rain road ship snow star tree wind", LessonSection.Words),
            L(13, "Частые слова", "A–Z", "Смотрите на следующее слово и старайтесь печатать с постоянным темпом.",
                "the of and to in is you that it he was for on are as with his they at be this have from or one had by word but not what all were we when your can said there use an each which she do how their if will up other about out many then them these so some her would make like him into time has look two more write go see number no way could people my than first been call who now find long down day did get come made may part", LessonSection.Words),
            L(14, "Буквосочетания", "TH SH CH WH QU", "Печатайте сочетания букв одним плавным движением.",
                "the thin thread|a thick cloth|three things|through the woods|father and mother|this and that|a sharp shell|short and shiny|fresh fish|a silver dish|a chat at church|a cheap chair|watch the match|white wheat|which way|where and when|quick and quiet|quite a change|a square quilt|a cheerful child|a wide wheel|a shallow shore|thank them both|a quick question|the right choice", LessonSection.Words),
            L(21, "Длинные слова", "8 букв и больше", "Переводите взгляд вперёд, чтобы набирать длинные слова без остановок.",
                "keyboard sunshine mountain newspaper afternoon bookshelf classroom direction landscape adventure butterfly pineapple strawberry dictionary photograph imagination temperature conversation unforgettable understanding independent vocabulary information neighborhood comfortable unexpected connection curiosity challenge patience", LessonSection.Words),
            L(16, "Цифры и время", "0–9", "Используйте цифровой ряд, чтобы сохранить положение пальцев на клавиатуре.",
                "0123456789|9876543210|12 34 56 78 90|Meet at 09:15|Train 204 arrives at 18:42|Lesson 7 starts soon|Turn at house 35|Open at 08:00|The book has 256 pages|Take route 124|Wait 45 minutes|Walk 600 meters|Code 740813|The score was 3:2|Buy 2 cups and 3 spoons|Room 504 is on floor 5|Try 24.11.2026|Pick seats 5, 6 and 8|Leaves at 06:55|Ready at 16:20", LessonSection.Symbols),
            L(22, "Клавиши со знаками", "@ # · [ ] · + =", "Сверяйтесь с подсветкой клавиш и нажимайте Shift нужной рукой.",
                "! ? . ,|: ; , .|( ) ( )|[ ] { }|+ - =|@ # $|% ^ &|* * +|<> <>|[7] {8}|!? ?!|-_ _-|:: ;;|1+2=3|5-3=2|50% 25%|$12.99|#7 #14|(2+8)*3|3.5 0.6|10:45 08:15|3:2 2:3|07/12 08/21|+7 -5", LessonSection.Symbols),
            L(17, "Пунктуация", ".,!?;:'\"", "Сверяйте запятую, точку, апостроф и кавычки с экранной клавиатурой.",
                "Wait, the bus is here!|Do you hear the rain?|It's a little late.|She said, \"Come in.\"|Tea, bread, fruit.|Bring this: a coat and a map.|The room is warm; sit here.|Try again, please.|I'll be there at 10:30.|No, that's my bag.|Where did you put it?|Check the list: milk, rice, and tea.|Hang on; the door is open.|\"That's enough,\" he said.|Two green apples and one pear.|Is that yours, or mine?|Look, the sky is clear!", LessonSection.Symbols),
            L(18, "Реплики и переписка", "A–Z · ? !", "Держите ритм, набирая короткие вопросы и ответы.",
                "Hi! Are you on your way?|Yes, I will be there in ten minutes.|Can you send me the address again?|Sure, it's 18 Oak Street.|Please wait by the side door.|Fine, I'll call when I arrive.|Shall I bring tea or coffee?|Bring tea, if you have some.|Is there still time for a walk?|Yes, we can leave at 14:00.|I left a note on the table.|Thanks, I'll read it later.|Where did my notebook go?|Try looking on the top shelf.|Oh, there it is!|See you tomorrow morning.", LessonSection.Texts),
            L(19, "Связный текст", "A–Z · 0–9 · ЗНАКИ", "Переводите взгляд на несколько букв вперёд, чтобы печатать без резких пауз.",
                "At 7 in the morning we walked down to the river. Mist hung above the water.|A clock ticked beside the open window. I set my book down and listened.|I folded the map and carried on along the shore. A bridge came into view.|Our neighbor opened the gate and called the dog. It ran straight into the yard.|The room smelled of toast and mint tea. We pulled our chairs up to the table.|When the wind fell, the moon rose over the trees. We turned off our lanterns.|The train arrived on time. We reached the meeting early and made tea.|On the way home we stopped beside a small lake. A boat drifted past.|By afternoon the clouds had drifted away. We set out for the hill.|A bright scarf hung from the back of a chair. I knew at once who had left it.|The wet road reflected the lights of the town. We walked beside it until sunset.|The library opens at 09:40 on Saturdays. I usually arrive just before ten.|He picked up the little tin box. Inside were three old photographs.|We moved the table nearer the window. By noon the whole room was bright.", LessonSection.Texts),
            new Lesson("23", "Сонеты Шекспира", Poetry.Shakespeare)
        }, "In spring the garden fills with birds, and we open the windows.|The evening tram slowed as it turned the corner near the park.|On the way to the sea, we stopped at a little bakery.|There were two maps and a small compass on the table.|Where does the trail to the tower begin?|After 15 minutes the sun came up over the fields.|Anna left a bag of green apples by the door.|I read the letter again and put it beside the window.|Start at a comfortable pace, then try to type a little faster.|In the morning the library opens at 09:40.|The lights on the water looked like rows of candles.|The road grew narrow and quiet after the bend.|The room smelled faintly of mint and pine.|By noon the clouds had cleared, and we could see the hills.|The next stop is ours, don't forget your umbrella!|Sam reached for his cup and opened a notebook.|The shop is at 46 West Street, near the school.|A neighbor swept snow from the front steps before breakfast.|The dog heard a sound but stayed by the door.|We found a quiet patch of grass and stopped for lunch.|A squirrel hid a nut and ran up the tallest tree.|Call me after 18:00, when you are back home.|It rained all day, but by sunset the sky was clear.|Which route is shorter, through the market or along the river?|Three blocks later we turned onto a wide street.|Someone had drawn a tiny boat on the cover of the book.|We could hear soft voices at the end of the hall.|An old photo of the harbor hung on the wall.|I sat by the window to watch the first lights come on.|A yellow leaf drifted onto the bench beside her.");
    }
}
