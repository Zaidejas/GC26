using System.Collections.Generic;

namespace GC26._2048
{
    public enum CheeseLanguage
    {
        English = 0,
        Russian = 1
    }

    /// <summary>
    /// All player-facing text in English and Russian. English is the default.
    /// Plain C# (no Unity Localization package: RULES.md forbids adding packages).
    /// </summary>
    public class CheeseLocale
    {
        // key → { English, Russian }
        static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            ["title"] = new[] { "Mousey's Cheese", "Мышкин сыр" },
            ["eyebrow"] = new[] { "FARM · PUZZLE", "ФЕРМА · ГОЛОВОЛОМКА" },
            ["best_caps"] = new[] { "BEST", "РЕКОРД" },
            ["best_item_caps"] = new[] { "BEST ITEM", "ЛУЧШИЙ ПРЕДМЕТ" },
            ["continue"] = new[] { "Continue", "Продолжить" },
            ["play"] = new[] { "Play", "Играть" },
            ["new_game"] = new[] { "New game", "Новая игра" },
            ["rules"] = new[] { "Rules", "Правила" },
            ["settings"] = new[] { "Settings", "Настройки" },
            ["exit"] = new[] { "Exit", "Выход" },
            ["facts"] = new[] { "4×4 field · 8 items · 150 moves", "Поле 4×4 · 8 предметов · 150 ходов" },
            ["menu"] = new[] { "Menu", "Меню" },
            ["score_caps"] = new[] { "SCORE", "ОЧКИ" },
            ["moves_caps"] = new[] { "MOVES", "ХОДЫ" },
            ["hole_caps"] = new[] { "HOLE", "НОРКА" },
            ["hint"] = new[] { "Swipe the field or use the arrows", "Свайп по полю или стрелки" },
            ["restart"] = new[] { "Restart", "Заново" },
            ["resume"] = new[] { "Resume", "Продолжить" },
            ["main_menu"] = new[] { "Main menu", "В главное меню" },
            ["sound"] = new[] { "Sound", "Звук" },
            ["on"] = new[] { "On", "Вкл" },
            ["off"] = new[] { "Off", "Выкл" },
            ["language"] = new[] { "Language", "Язык" },
            ["language_name"] = new[] { "English", "Русский" },
            ["close"] = new[] { "Close", "Закрыть" },
            ["got_it"] = new[] { "Got it", "Понятно" },
            ["rules_text"] = new[]
            {
                "1. Swipe up, down, left or right — every item on the field slides.\n" +
                "2. Two identical items merge into the next one.\n" +
                "3. Every merge scores points: the more valuable the item, the more points — from 32 to 2048.\n" +
                "4. The mouse takes finished cheese to its hole right away, and the cell is freed.\n" +
                "5. After every move, new grass or hay appears on the field.\n" +
                "6. You have 150 moves. The game ends when you run out of moves or the field is full.",
                "1. Проведите пальцем вверх, вниз, влево или вправо — все предметы на поле сдвинутся.\n" +
                "2. Два одинаковых предмета объединяются в следующий.\n" +
                "3. За каждое объединение — очки: чем ценнее предмет, тем больше, от 32 до 2048.\n" +
                "4. Готовый сыр мышка сразу уносит в норку, и место освобождается.\n" +
                "5. После каждого хода на поле появляется новая трава или сено.\n" +
                "6. У вас 150 ходов. Игра заканчивается, когда ходы кончатся или поле заполнится."
            },
            ["chain_first"] = new[] { "appears on the field", "появляется на поле" },
            ["chain_to_hole"] = new[] { " → the mouse takes it home", " → мышка уносит в норку" },
            ["confirm_title"] = new[] { "Start over?", "Начать заново?" },
            ["confirm_text"] = new[] { "Your current game will be lost.", "Текущая партия будет потеряна." },
            ["confirm_yes"] = new[] { "Start a new game", "Начать новую партию" },
            ["cancel"] = new[] { "Cancel", "Отмена" },
            ["out_of_moves"] = new[] { "Out of moves", "Ходы закончились" },
            ["field_full"] = new[] { "The field is full", "Поле заполнено" },
            ["new_record"] = new[] { "New record!", "Новый рекорд!" },
            ["score"] = new[] { "Score", "Очки" },
            ["best"] = new[] { "Best", "Рекорд" },
            ["best_item"] = new[] { "Best item", "Лучший предмет" },
            ["cheese_in_hole"] = new[] { "Cheese in the hole", "Сыров в норке" },
            ["moves_made"] = new[] { "Moves made", "Сделано ходов" },
            ["cheese_ready"] = new[] { "Cheese is ready!", "Сыр готов!" },
        };

        // One name per item, the same on tiles, in the rules and on the results screen.
        // index = item level, then { English, Russian }
        static readonly string[][] ItemNames =
        {
            new[] { "", "" },
            new[] { "Grass", "Трава" },
            new[] { "Hay", "Сено" },
            new[] { "Cow", "Корова" },
            new[] { "Milk can", "Бидон" },
            new[] { "Bottle", "Бутылка" },
            new[] { "Sour cream", "Сметана" },
            new[] { "Curd", "Творог" },
            new[] { "Cheese", "Сыр" },
        };

        public CheeseLanguage Language { get; private set; }

        public CheeseLocale(CheeseLanguage language = CheeseLanguage.English)
        {
            Language = language;
        }

        public void Set(CheeseLanguage language) => Language = language;

        public CheeseLanguage Next => Language == CheeseLanguage.English ? CheeseLanguage.Russian : CheeseLanguage.English;

        public string this[string key] => Get(key, Language);

        public string ItemName(int level) => ItemName(level, Language);

        public static IEnumerable<string> Keys => Texts.Keys;

        public static string Get(string key, CheeseLanguage language) =>
            Texts.TryGetValue(key, out var v) ? v[(int)language] : key;

        public static string ItemName(int level, CheeseLanguage language) =>
            level > 0 && level < ItemNames.Length ? ItemNames[level][(int)language] : "";
    }
}
