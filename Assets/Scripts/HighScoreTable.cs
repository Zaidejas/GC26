using System.Collections.Generic;
using UnityEngine;

// Таблица рекордов «как на автомате»: 5 строк с именем до 8 символов.
// Хранится в PlayerPrefs строкой "AAA:12345|BBB:999|...". Статических полей нет:
// между запусками Play без перезагрузки домена ничего не залипает
public static class HighScoreTable
{
    public const int Size = 5;
    public const string TableKey = "HighScoreTable";
    public const string LastInitialsKey = "LastInitials";

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

    public static List<Entry> Load()
    {
        var list = new List<Entry>();
        string raw = PlayerPrefs.GetString(TableKey, "");

        if (string.IsNullOrEmpty(raw))
        {
            // Старый рекорд без имени переносим в таблицу
            int old = PlayerPrefs.GetInt(GameManager.HighScoreKey, 0);
            if (old > 0) list.Add(new Entry("???", old));
            return list;
        }

        foreach (string part in raw.Split('|'))
        {
            string[] pair = part.Split(':');
            if (pair.Length == 2 && int.TryParse(pair[1], out int score))
                list.Add(new Entry(pair[0], score));
        }
        list.Sort((a, b) => b.score.CompareTo(a.score));
        return list;
    }

    public static int Best
    {
        get
        {
            var list = Load();
            return list.Count > 0 ? list[0].score : 0;
        }
    }

    // Вставляет результат. Возвращает место (0..4) или -1, если в топ не попал
    public static int Insert(string initials, int score)
    {
        var list = Load();
        int index = list.FindIndex(e => score > e.score);
        if (index < 0) index = list.Count;
        if (index >= Size || score <= 0) return -1;

        list.Insert(index, new Entry(initials, score));
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        Save(list);
        return index;
    }

    public static void Rename(int index, string initials)
    {
        var list = Load();
        if (index < 0 || index >= list.Count) return;
        list[index] = new Entry(initials, list[index].score);
        Save(list);
    }

    static void Save(List<Entry> list)
    {
        var parts = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            parts[i] = $"{list[i].initials}:{list[i].score}";
        PlayerPrefs.SetString(TableKey, string.Join("|", parts));
        // Меню показывает BEST по старому ключу - держим его равным первой строке
        PlayerPrefs.SetInt(GameManager.HighScoreKey, list.Count > 0 ? list[0].score : 0);
        PlayerPrefs.Save();
    }
}
