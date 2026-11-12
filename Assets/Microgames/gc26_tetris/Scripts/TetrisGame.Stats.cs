using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Lifetime statistics: counted during play, saved at the end of a game, shown from the main menu.
    /// The counters live in PlayStats.
    /// </summary>
    public sealed partial class TetrisGame
    {
        const string StatsName = "Stats";

        PlayStats playStats = new PlayStats();
        float playClock;
        bool inStats;
        int statsIndex;
        readonly float[] statsGlow = { 1f };

        void LoadStats()
        {
            playStats = PlayStats.Parse(PlayerPrefs.GetString(recordsPrefix + StatsName, ""));
            // Achievements started counting earlier, so their numbers are a floor for the same things here.
            playStats.Raise(PlayStat.Lines, achievements.Value(AchievementStat.LinesTotal));
            playStats.Raise(PlayStat.BestCombo, achievements.Value(AchievementStat.Combo));
            playStats.Raise(PlayStat.BestLevel, achievements.Value(AchievementStat.Level));
            playStats.Raise(PlayStat.Bombs, achievements.Value(AchievementStat.Bombs));
            playStats.Raise(PlayStat.Lightnings, achievements.Value(AchievementStat.Lightnings));
            playStats.Raise(PlayStat.Bots, achievements.Value(AchievementStat.BotsTotal));
            playStats.Raise(PlayStat.AttacksBlocked, achievements.Value(AchievementStat.AttacksBlocked));
        }

        void SaveStats()
        {
            if (autoplay || noRecords)
                return;
            PlayerPrefs.SetString(recordsPrefix + StatsName, playStats.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Counts in memory only; SaveStats writes at the end of a game, so this is cheap to call often.</summary>
        void CountStat(PlayStat stat, long amount)
        {
            if (autoplay || noRecords)
                return;
            playStats.Add(stat, amount);
        }

        void CountPlayTime(float deltaTime)
        {
            playClock += deltaTime;
            int whole = (int)playClock;
            if (whole <= 0)
                return;
            CountStat(PlayStat.Seconds, whole);
            playClock -= whole;
        }

        // --- the screen -----------------------------------------------------------------------

        void OpenStats()
        {
            Debug.Log("[BlockDrop] Stats opened: " + playStats.Serialize());
            inStats = true;
            statsIndex = 0;
            gameAudio.Play("hold");
        }

        void CloseStats()
        {
            Debug.Log("[BlockDrop] Stats closed");
            inStats = false;
            gameAudio.Play("hold", 0.6f);
        }

        void UpdateStatsScreen()
        {
            menuClock += Time.deltaTime;
            if (Pressed(Key.Escape) || Pressed(Key.Backspace))
            {
                CloseStats();
                return;
            }
            if (Pressed(Key.L))
                SetLanguage(!english);
            if (HandleButtons(ref statsIndex, 1, HudLayout.AchievementsBackY, 1f, 6f, 1.1f))
                CloseStats();
        }

        struct StatsRow
        {
            public string Name;
            public string Value;
            public int Tile;

            public StatsRow(string name, string value, int tile)
            {
                Name = name;
                Value = value;
                Tile = tile;
            }
        }

        StatsRow[] StatsRows()
        {
            return new[]
            {
                new StatsRow(T("Марафонов сыграно", "Marathons played"), Number(PlayStat.MarathonGames), 2),
                new StatsRow(T("Боёв сыграно", "Battles played"), Number(PlayStat.Battles), 2),
                new StatsRow(T("Время в игре", "Time played"), Duration(playStats.Get(PlayStat.Seconds)), 2),
                new StatsRow(T("Фигур поставлено", "Pieces placed"), Number(PlayStat.Pieces), 2),
                new StatsRow(T("Линий очищено", "Lines cleared"), Number(PlayStat.Lines), 6),
                new StatsRow(T("Четыре линии разом", "Four-line clears"), Number(PlayStat.Fours), 6),
                new StatsRow(T("Лучшее комбо", "Best combo"), Number(PlayStat.BestCombo), 6),
                new StatsRow(T("Лучший уровень", "Best level"), Number(PlayStat.BestLevel), 6),
                new StatsRow(T("Рекорд марафона", "Marathon best"), Best(marathonTable).ToString("N0"), 3),
                new StatsRow(T("Рекорд боя", "Battle best"), Best(battleTable).ToString("N0"), 3),
                new StatsRow(T("Бомб взорвано", "Bombs set off"), Number(PlayStat.Bombs), 0),
                new StatsRow(T("Ударов молнии", "Lightning strikes"), Number(PlayStat.Lightnings), 0),
                new StatsRow(T("Ботов повержено", "Bots beaten"), Number(PlayStat.Bots), 1),
                new StatsRow(T("Атак отбито", "Attacks blocked"), Number(PlayStat.AttacksBlocked), 1),
                new StatsRow(T("Рядов мусора получено", "Garbage rows taken"), Number(PlayStat.GarbageRows), 1),
                new StatsRow(T("Монет заработано", "Coins earned"), Number(PlayStat.Coins), 4),
            };
        }

        string Number(PlayStat stat)
        {
            return playStats.Get(stat).ToString("N0");
        }

        string Duration(long seconds)
        {
            if (seconds < 60)
                return seconds + T(" с", " s");
            long minutes = seconds / 60;
            if (minutes < 60)
                return minutes + T(" мин", " min");
            return (minutes / 60).ToString("N0") + T(" ч ", " h ") + (minutes % 60).ToString("00") + T(" мин", " min");
        }

        void DrawStatsScreen(float unit)
        {
            FillWorld(0f, 0f, 200f, 200f, new Color(0.07f, 0.055f, 0.1f, 1f));
            DrawMenuBackground();

            Label(T("СТАТИСТИКА", "STATISTICS"), 0f, HudLayout.AchievementsTitleY, unit, 1.15f, TextAnchor.MiddleCenter, view.AccentColor);
            Label(T("за все игры на этом компьютере", "all games on this computer"), 0f, HudLayout.AchievementsCountY, unit, 0.45f, TextAnchor.MiddleCenter, Muted);

            StatsRow[] rows = StatsRows();
            int columns = HudLayout.StatsColumns;
            int perColumn = (rows.Length + columns - 1) / columns;
            for (int i = 0; i < rows.Length; i++)
            {
                int column = i / perColumn, line = i % perColumn;
                DrawStatsRow(rows[i], HudLayout.StatsColumnX(column), HudLayout.StatsTopY - line * HudLayout.StatsStepY, unit);
            }

            DrawButton(T("Назад", "Back"), null, HudLayout.AchievementsBackY, 6f, 1.1f, statsGlow[0], 0, unit);
            Label(T("Esc назад", "Esc back"), 0f, HudLayout.MenuHintY, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
        }

        void DrawStatsRow(StatsRow row, float x, float y, float unit)
        {
            float width = HudLayout.StatsRowWidth, height = HudLayout.StatsRowHeight;
            RoundedWorld(x, y, width, height, new Color(0.12f, 0.1f, 0.18f, 0.95f), 0.3f, 0f, unit);

            float left = x - width / 2f;
            Color tile = view.PieceColor(row.Tile);
            DrawTile(left + 0.55f, y, 0.45f, tile);

            // Label centres a 26-unit box on x, so the left edge of the text is x - 13 and the right edge x + 13.
            float size = HudLayout.Portrait ? 0.4f : 0.42f;
            Label(row.Name, left + 1.05f + 13f, y, unit, size, TextAnchor.MiddleLeft, Muted);
            Label(row.Value, x + width / 2f - 0.4f - 13f, y, unit, size * 1.15f, TextAnchor.MiddleRight, Bright);
        }
    }
}
