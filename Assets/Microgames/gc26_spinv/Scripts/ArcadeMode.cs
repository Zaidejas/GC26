using UnityEngine;

namespace GC26.Spinv
{
    // Аркадный режим открывается прохождением игры, какое бы имя игрок потом ни ввёл.
    // Всё хранится в PlayerPrefs, статического состояния нет (Play без перезагрузки домена)
    public static class ArcadeMode
    {
        const string UnlockedKey = SpinvMicrogame.Id + "_ArcadeUnlocked";
        const string SelectedKey = SpinvMicrogame.Id + "_ArcadeSelected";
        public const string HighScoreKey = SpinvMicrogame.Id + "_ArcadeHighScore";

        public static bool IsUnlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;

        public static void Unlock()
        {
            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.Save();
        }

        // Какой режим выбран в меню: сцена игры одна, она смотрит сюда при запуске
        public static bool IsSelected => IsUnlocked && PlayerPrefs.GetInt(SelectedKey, 0) == 1;

        public static void Select(bool arcade)
        {
            PlayerPrefs.SetInt(SelectedKey, arcade ? 1 : 0);
        }

        // Личный рекорд в аркаде: показывается в меню рядом с обычным
        public static int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

        // Полный сброс: забыть открытие аркады, выбранный режим и рекорд аркады
        public static void DeleteKeys()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(SelectedKey);
            PlayerPrefs.DeleteKey(HighScoreKey);
        }
    }
}
