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
            ["score"] = "SCORE", ["next"] = "NEXT", ["undo"] = "Undo", ["reset"] = "Reset", ["noPath"] = "No path",
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
            ["settings"] = "Settings", ["music"] = "Music", ["sounds"] = "Sounds", ["vibration"] = "Vibration",
            ["language"] = "Language",
        };

        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["tagline"] = "Собери пять в ряд", ["continue"] = "Продолжить", ["scoreShort"] = "счёт",
            ["newGame"] = "Новая игра", ["howToPlay"] = "Как играть", ["leaderboard"] = "Рекорды",
            ["score"] = "СЧЁТ", ["next"] = "СЛЕДУЮЩИЕ", ["undo"] = "Отменить", ["reset"] = "Заново", ["noPath"] = "Нет пути",
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
