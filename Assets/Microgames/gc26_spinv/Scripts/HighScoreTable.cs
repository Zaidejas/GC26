using System.Collections.Generic;
using UnityEngine;

namespace GC26.Spinv
{
    // Таблица рекордов «как на автомате»: 5 строк с именем до 8 букв.
    // Первое место навсегда закреплено за Gekata (-999999): его не сместить никаким результатом,
    // игроки занимают места 2-5 по убыванию очков. В PlayerPrefs хранятся только строки игроков:
    // "AAA:12345|BBB:999|...". Личный рекорд игрока (меню, HI) хранится отдельно в SpinvGameManager.HighScoreKey.
    // Статических полей нет: между запусками Play без перезагрузки домена ничего не залипает
    public static class HighScoreTable
    {
        public const int Size = 5;
        public const string TableKey = SpinvMicrogame.Id + "_HighScoreTable";
        public const string LastInitialsKey = SpinvMicrogame.Id + "_LastInitials";

        // Имя результата, пока игрок не ввёл своё на экране победы
        public const string UnnamedPlayer = "???";

        // Имя автора занято закреплённой строкой: игрок не может записаться под ним (без учёта регистра)
        public static bool IsReservedName(string name) =>
            string.Equals(name?.Trim(), PinnedName, System.StringComparison.OrdinalIgnoreCase);

        // Закреплённая первая строка
        public const string PinnedName = "Gekata";
        public const int PinnedScore = -999999;

        public struct Entry
        {
            public string initials;
            public int score;
            public Entry(string initials, int score) { this.initials = initials; this.score = score; }
        }

        public static string LastInitials
        {
            get => PlayerPrefs.GetString(LastInitialsKey, "AAA");
            set => PlayerPrefs.SetString(LastInitialsKey, value);
        }

        // Полный сброс сохранений. Чтобы сбросить ещё раз на всех устройствах - увеличить SeedVersion:
        // при первом запуске новой версии игра как после первой установки (Gekata в таблице остаётся всегда)
        public const int SeedVersion = 9;
        public const string SeedVersionKey = SpinvMicrogame.Id + "_HighScoreSeedVersion";

        static void ApplySeed()
        {
            if (PlayerPrefs.GetInt(SeedVersionKey, 0) < SeedVersion)
                WipeAll();
        }

        // Стирает всё наше: таблицу, рекорды, монеты, аркаду и достижения. Только ключи gc26_spinv_:
        // проект общий, сохранения других игр не трогаем (RULES.md)
        public static void WipeAll()
        {
            PlayerPrefs.DeleteKey(TableKey);
            PlayerPrefs.DeleteKey(LastInitialsKey);
            PlayerPrefs.DeleteKey(SpinvGameManager.HighScoreKey);
            PlayerPrefs.DeleteKey(SpinvGameManager.TotalCoinsKey);
            PlayerPrefs.DeleteKey(SpinvGameManager.LifetimeCoinsKey);
            ArcadeMode.DeleteKeys();
            Achievements.DeleteKeys();
            PlayerPrefs.SetInt(SeedVersionKey, SeedVersion);
            PlayerPrefs.Save();
        }

        // Строки игроков без закреплённой, по убыванию очков, не больше Size - 1
        static List<Entry> LoadPlayers()
        {
            ApplySeed();
            var list = new List<Entry>();
            string raw = PlayerPrefs.GetString(TableKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split('|'))
                {
                    string[] pair = part.Split(':');
                    if (pair.Length == 2 && int.TryParse(pair[1], out int score))
                        list.Add(new Entry(pair[0], score));
                }
            }
            list.Sort((a, b) => b.score.CompareTo(a.score));
            if (list.Count > Size - 1) list.RemoveRange(Size - 1, list.Count - (Size - 1));
            return list;
        }

        // Вся таблица как на экране: Gekata первым, дальше игроки
        public static List<Entry> Load()
        {
            var list = LoadPlayers();
            list.Insert(0, new Entry(PinnedName, PinnedScore));
            return list;
        }

        // Вставляет результат игрока. Возвращает место в таблице (1..4) или -1, если не попал
        public static int Insert(string initials, int score)
        {
            if (IsReservedName(initials)) initials = UnnamedPlayer;
            if (score <= 0) return -1;
            var players = LoadPlayers();
            int index = players.FindIndex(e => score > e.score);
            if (index < 0) index = players.Count;
            if (index >= Size - 1) return -1;

            players.Insert(index, new Entry(initials, score));
            if (players.Count > Size - 1) players.RemoveRange(Size - 1, players.Count - (Size - 1));
            Save(players);
            return index + 1; // строка 0 - Gekata
        }

        // index - место в таблице, как вернул Insert
        public static void Rename(int index, string initials)
        {
            if (IsReservedName(initials)) return;
            var players = LoadPlayers();
            int i = index - 1;
            if (i < 0 || i >= players.Count) return;
            players[i] = new Entry(initials, players[i].score);
            Save(players);
        }

        // Убирает строку игрока (index - место в таблице, как вернул Insert). Gekata не трогается
        public static void Remove(int index)
        {
            var players = LoadPlayers();
            int i = index - 1;
            if (i < 0 || i >= players.Count) return;
            players.RemoveAt(i);
            Save(players);
        }

        static void Save(List<Entry> players)
        {
            var parts = new string[players.Count];
            for (int i = 0; i < players.Count; i++)
                parts[i] = $"{players[i].initials}:{players[i].score}";
            PlayerPrefs.SetString(TableKey, string.Join("|", parts));
            PlayerPrefs.Save();
        }
    }
}
