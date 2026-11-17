using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GC26.Lines.UI
{
    /// <summary>The languages the game speaks (Settings → Language).</summary>
    public enum Language { English, Russian }

    /// <summary>
    /// Every text the player sees, in English and Russian (GDD → Settings; the prototype's I18N table).
    /// Screens read their texts from here and refresh them when <see cref="Changed"/> fires.
    /// The current language is set by <c>GameBootstrap</c> from the saved settings each time the game starts.
    /// </summary>
    public static class Texts
    {
        public static Language Current { get; private set; } = Language.English;

        /// <summary>The language changed: screens put their texts in the new language.</summary>
        public static event Action Changed;

        public static void Set(Language language)
        {
            if (language == Current) return;
            Current = language;
            Changed?.Invoke();
        }

        /// <summary>Russian on a phone set to Russian (or Belarusian/Ukrainian, which usually read Russian), else English.</summary>
        public static Language FromSystem(SystemLanguage system) =>
            system == SystemLanguage.Russian || system == SystemLanguage.Belarusian || system == SystemLanguage.Ukrainian
                ? Language.Russian : Language.English;

        /// <summary>The short code saved in the settings file ("en", "ru").</summary>
        public static string Code(Language language) => language == Language.Russian ? "ru" : "en";

        /// <summary>The language for a saved code, or null for an unknown or empty one.</summary>
        public static Language? FromCode(string code) =>
            code == "ru" ? Language.Russian : code == "en" ? Language.English : (Language?)null;

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["tagline"] = "Line up five of a kind", ["continue"] = "Continue", ["scoreShort"] = "score",
            ["newGame"] = "New game", ["howToPlay"] = "How to play", ["leaderboard"] = "Leaderboard",
            ["score"] = "SCORE", ["next"] = "NEXT", ["undo"] = "Undo", ["reset"] = "Reset", ["noPath"] = "No path", ["newBestShort"] = "New best!",
            ["confirmTitle"] = "Start a new game?", ["confirmText"] = "Your current game will be lost.",
            ["cancel"] = "Cancel", ["yes"] = "Yes",
            ["gameOver"] = "Game over", ["finalScore"] = "Final score", ["nameLabel"] = "Your name for the leaderboard",
            ["save"] = "Save", ["player"] = "Player",
            ["back"] = "Back", ["nextButton"] = "Next", ["gotIt"] = "Got it",
            ["howto1Title"] = "Move a piece",
            ["howto1Text"] = "Tap a piece, then an empty cell. It moves only if there is a free path up, down, left or right.",
            ["howto2Title"] = "Make lines of five",
            ["howto2Text"] = "Five or more of the same kind in a row, column or diagonal disappear and score points. Longer lines score more.",
            ["howto3Title"] = "Watch what's coming",
            ["howto3Text"] = "If your move makes no line, 3 new pieces arrive where the faded mushrooms show. The game ends when the board is full. Undo takes back one move.",
            ["scoresEmpty"] = "No finished games yet. Your score is added here when a game ends.",
            ["playAgain"] = "Play again",
            // L09 Timed mode
            ["classic"] = "Classic", ["timed"] = "Timed", ["timeUp"] = "Time's up!", ["secondsShort"] = "s",
            ["howto4Title"] = "Race the clock",
            ["howto4Text"] = "Choose Timed on the menu to play against the clock: 3 minutes, and every cleared line adds 5 seconds. The clock stops while mushrooms move.",
            ["zeroHint"] = "Clear a line to get on the leaderboard.", ["menuButton"] = "Menu",
            // L14–L19: the special mushrooms, their tutorials, Pause (GDD → Special mushrooms, Tutorials on the board, Timed mode)
            ["howto5Title"] = "Rainbow mushroom",
            ["howto5Text"] = "The rare rainbow mushroom fits any line: it counts as every colour at once. One rainbow can even finish two lines of different colours in a single move.",
            ["howto6Title"] = "Frozen mushrooms",
            ["howto6Text"] = "From 100 points, some new mushrooms arrive frozen. They can't be moved, but they still count for their colour: make a line through one to break the ice.",
            ["howto7Title"] = "Puffball bomb",
            ["howto7Text"] = "Every 200 points earn a puffball (you can keep 2). Tap it below the board: a 3×3 square lights up. Tap another cell to move it, and tap the lit spot to burst. No new mushrooms come after a burst.",
            ["frozen"] = "Frozen — can't move",
            ["frozenHint"] = "Make a line through it to break the ice",
            ["superName"] = "Puffball",
            ["superGot"] = "A puffball for you!",
            ["superGotText"] = "Tap it below the board to use it",
            ["superNothing"] = "Nothing to burst here",
            ["superRule"] = "You get one every 200 points and can keep 2",
            ["aimTitle"] = "Where to burst?",
            ["aimText"] = "Tap another cell to move it, tap the lit spot to burst. The puffball button cancels.",
            ["coachRainbow"] = "A rainbow mushroom: it fits any line",
            ["coachIce"] = "Frozen: it can't move. Make a line through it to break the ice",
            ["coachSuper1"] = "You earned a puffball!",
            ["coachSuper1b"] = "Tap it to use it",
            ["coachAimTitle"] = "Where to burst?",
            ["coachSuper2"] = "Tap another cell to move it, tap the lit spot to burst",
            ["skip"] = "Skip",
            ["rescueFull"] = "The board is full!",
            ["rescueNoMoves"] = "No moves left!",
            ["rescueSub"] = "Use your puffball to keep going",
            ["endGame"] = "End game",
            ["noMovesNote"] = "No moves left. Final score",
            ["pause"] = "Pause",
            ["paused"] = "Paused",
            ["pausedText"] = "The clock is stopped and the board is hidden.",
            ["resume"] = "Resume",
            ["backToGame"] = "Back to the game",
            ["settings"] = "Settings", ["music"] = "Music", ["sounds"] = "Sounds", ["vibration"] = "Vibration",
            ["language"] = "Language",
        };

        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["tagline"] = "Собери пять в ряд", ["continue"] = "Продолжить", ["scoreShort"] = "счёт",
            ["newGame"] = "Новая игра", ["howToPlay"] = "Как играть", ["leaderboard"] = "Рекорды",
            ["score"] = "СЧЁТ", ["next"] = "СЛЕДУЮЩИЕ", ["undo"] = "Отменить", ["reset"] = "Заново", ["noPath"] = "Нет пути", ["newBestShort"] = "Новый рекорд!",
            ["confirmTitle"] = "Начать новую игру?", ["confirmText"] = "Текущая игра будет потеряна.",
            ["cancel"] = "Отмена", ["yes"] = "Да",
            ["gameOver"] = "Игра окончена", ["finalScore"] = "Итоговый счёт", ["nameLabel"] = "Твоё имя для таблицы рекордов",
            ["save"] = "Сохранить", ["player"] = "Игрок",
            ["back"] = "Назад", ["nextButton"] = "Далее", ["gotIt"] = "Понятно",
            ["howto1Title"] = "Передвинь гриб",
            ["howto1Text"] = "Нажми на гриб, затем на пустую клетку. Он переместится, только если есть свободный путь вверх, вниз, влево или вправо.",
            ["howto2Title"] = "Собирай линии из пяти",
            ["howto2Text"] = "Пять и больше одинаковых грибов в ряд, столбец или по диагонали исчезают и приносят очки. Чем длиннее линия, тем больше очков.",
            ["howto3Title"] = "Следи, что дальше",
            ["howto3Text"] = "Если ход не собрал линию, появятся 3 новых гриба — там, где видны бледные грибы. Игра заканчивается, когда поле заполнено. «Отменить» возвращает один ход.",
            ["scoresEmpty"] = "Пока нет законченных игр. Твой счёт появится здесь, когда игра закончится.",
            ["playAgain"] = "Сыграть ещё",
            // L09 Timed mode
            ["classic"] = "Классика", ["timed"] = "На время", ["timeUp"] = "Время вышло!", ["secondsShort"] = "с",
            ["howto4Title"] = "Наперегонки со временем",
            ["howto4Text"] = "Выбери в меню «На время», чтобы играть на скорость: 3 минуты, и каждая собранная линия добавляет 5 секунд. Пока грибы двигаются, часы стоят.",
            ["zeroHint"] = "Собери линию, чтобы попасть в таблицу рекордов.", ["menuButton"] = "Меню",
            // L14–L19: the special mushrooms, their tutorials, Pause (GDD → Special mushrooms, Tutorials on the board, Timed mode)
            ["howto5Title"] = "Радужный гриб",
            ["howto5Text"] = "Редкий радужный гриб подходит к любой линии: он считается грибом любого цвета. Один радужный гриб может даже завершить две линии разных цветов за один ход.",
            ["howto6Title"] = "Замёрзшие грибы",
            ["howto6Text"] = "После 100 очков некоторые новые грибы появляются замёрзшими. Их нельзя передвинуть, но они считаются своим цветом: собери линию через такой гриб, чтобы разбить лёд.",
            ["howto7Title"] = "Дождевик-бомба",
            ["howto7Text"] = "Каждые 200 очков дают дождевик (можно хранить 2). Нажми на него под полем: загорится квадрат 3×3. Нажми другую клетку, чтобы передвинуть его, и нажми на отмеченное место, чтобы взорвать. После взрыва новые грибы не появляются.",
            ["frozen"] = "Замёрз — не двигается",
            ["frozenHint"] = "Собери через него линию, чтобы разбить лёд",
            ["superName"] = "Дождевик",
            ["superGot"] = "Тебе дождевик!",
            ["superGotText"] = "Нажми на него под полем, чтобы применить",
            ["superNothing"] = "Здесь нечего взрывать",
            ["superRule"] = "Он даётся каждые 200 очков, можно хранить 2",
            ["aimTitle"] = "Где взорвать?",
            ["aimText"] = "Нажми другую клетку, чтобы передвинуть; нажми на отмеченное место, чтобы взорвать. Кнопка дождевика — отмена.",
            ["coachRainbow"] = "Радужный гриб подходит к любой линии",
            ["coachIce"] = "Замёрз и не двигается. Собери через него линию, чтобы разбить лёд",
            ["coachSuper1"] = "Тебе дождевик!",
            ["coachSuper1b"] = "Нажми на него, чтобы применить",
            ["coachAimTitle"] = "Где взорвать?",
            ["coachSuper2"] = "Нажми другую клетку, чтобы передвинуть; нажми на отмеченное место, чтобы взорвать",
            ["skip"] = "Пропустить",
            ["rescueFull"] = "Поле заполнено!",
            ["rescueNoMoves"] = "Ходов больше нет!",
            ["rescueSub"] = "Взорви дождевик, чтобы играть дальше",
            ["endGame"] = "Закончить игру",
            ["noMovesNote"] = "Ходов больше нет. Итоговый счёт",
            ["pause"] = "Пауза",
            ["paused"] = "Пауза",
            ["pausedText"] = "Часы остановлены, а поле скрыто.",
            ["resume"] = "Продолжить",
            ["backToGame"] = "Назад к игре",
            ["settings"] = "Настройки", ["music"] = "Музыка", ["sounds"] = "Звуки", ["vibration"] = "Вибрация",
            ["language"] = "Язык",
        };

        /// <summary>The keys every language must have (tests check both tables against this).</summary>
        public static IEnumerable<string> Keys => En.Keys;

        /// <summary>The text for <paramref name="key"/> in <paramref name="language"/>, or null if that table lacks it.</summary>
        public static string In(Language language, string key) =>
            (language == Language.Russian ? Ru : En).TryGetValue(key, out var text) ? text : null;

        /// <summary>The text for <paramref name="key"/> in the current language (English if it's missing).</summary>
        public static string Get(string key) => In(Current, key) ?? In(Language.English, key) ?? key;

        /// <summary>The line above the leaderboard after saving a score (GDD → Scoring → Leaderboard).</summary>
        public static string ResultMessage(int place, int score, string name)
        {
            if (Current == Language.Russian)
            {
                if (place == 0) return "Новый рекорд, " + name + "!";
                if (place > 0) return "Ты на " + (place + 1) + "-м месте, " + name + "!";
                return "Счёт " + score + " не попал в топ-10";
            }
            if (place == 0) return "New best score, " + name + "!";
            if (place > 0) return "You're #" + (place + 1) + ", " + name + "!";
            return score + " points didn't make the top 10";
        }

        /// <summary>L16: the bubble over a puffball that isn't ready: "Puffball in 40 points" / "Дождевик через 40 очков".</summary>
        public static string PuffballIn(int points)
        {
            if (Current == Language.Russian)
            {
                int last = points % 10, lastTwo = points % 100;
                string word = last == 1 && lastTwo != 11 ? "очко" : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? "очка" : "очков";
                return "Дождевик через " + points + " " + word;
            }
            return "Puffball in " + points + (points == 1 ? " point" : " points");
        }

        // Our own short month names: phone builds may not carry the system's date data for every language.
        static readonly string[] MonthsEn = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        static readonly string[] MonthsRu = { "янв.", "февр.", "марта", "апр.", "мая", "июня", "июля", "авг.", "сент.", "окт.", "нояб.", "дек." };

        /// <summary>A leaderboard date: "21 Oct" or "21 окт.".</summary>
        /// <summary>Every letter the game can show in <paramref name="language"/> (texts, month names, digits, signs).</summary>
        public static string Letters(Language language)
        {
            var all = new System.Text.StringBuilder("0123456789+-·:.,!?%#№ ");
            foreach (var key in Keys) all.Append(In(language, key));
            foreach (var month in language == Language.Russian ? MonthsRu : MonthsEn) all.Append(month);
            var unique = new HashSet<char>();
            var letters = new System.Text.StringBuilder();
            foreach (char c in all.ToString())
                if (!char.IsControl(c) && unique.Add(c)) letters.Append(c);
            return letters.ToString();
        }

        public static string Date(long unixMilliseconds)
        {
            var d = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime();
            var months = Current == Language.Russian ? MonthsRu : MonthsEn;
            return d.Day.ToString(CultureInfo.InvariantCulture) + " " + months[d.Month - 1];
        }
    }
}
