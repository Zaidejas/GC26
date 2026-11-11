using UnityEngine;

namespace GC26.Spinv
{
    public enum AchievementId
    {
        EarthIsSaved, Flawless, BombSquad, Exorcist, Loop3, NightOwl,
        HighRoller, Shopaholic, PiggyBank, HandsOff, Impostor, Insomnia,
    }

    public class AchievementDef
    {
        public AchievementId id;
        public string title;
        public string description;
        // Можно получить только в аркаде: до её открытия достижение не показывается
        public bool arcadeOnly;
        // Скрытое: до получения вместо названия и описания показываются заглушки
        public bool secret;
    }

    // Достижения. Полученные хранятся в PlayerPrefs, изменяемого статического состояния нет
    public static class Achievements
    {
        public const string SecretTitle = "???";
        public const string SecretDescription = "The One Who Must Not Be Named";
        const string PendingKey = SpinvMicrogame.Id + "_AchievementPendingToasts";

        // Порядок совпадает с AchievementId
        public static readonly AchievementDef[] All =
        {
            Def(AchievementId.EarthIsSaved, "EARTH IS SAVED", "Finish Classic"),
            Def(AchievementId.Flawless, "FLAWLESS", "Finish Classic without losing a life"),
            Def(AchievementId.BombSquad, "BOMB SQUAD", "Defeat Bomber", true),
            Def(AchievementId.Exorcist, "EXORCIST", "Defeat Phantom", true),
            Def(AchievementId.Loop3, "LOOP 3", "Reach loop 3 in Arcade", true),
            Def(AchievementId.NightOwl, "NIGHT OWL", "Reach loop 5 in Arcade", true),
            Def(AchievementId.HighRoller, "HIGH ROLLER", "Score 15,000 in one Arcade run", true),
            Def(AchievementId.Shopaholic, "SHOPAHOLIC", "Use all 3 abilities in one wave"),
            Def(AchievementId.PiggyBank, "PIGGY BANK", "Collect 500 coins in total"),
            Def(AchievementId.HandsOff, "HANDS OFF", "Defeat Phantom without being caught by a hand", true),
            Def(AchievementId.Impostor, "IMPOSTOR", "You have been informed.", false, true),
            Def(AchievementId.Insomnia, "INSOMNIA", "Survive 3 DARK waves in a row", true),
        };

        static AchievementDef Def(AchievementId id, string title, string description, bool arcadeOnly = false, bool secret = false) =>
            new AchievementDef { id = id, title = title, description = description, arcadeOnly = arcadeOnly, secret = secret };

        static string Key(AchievementId id) => SpinvMicrogame.Id + "_Achievement_" + id;

        public static bool IsUnlocked(AchievementId id) => PlayerPrefs.GetInt(Key(id), 0) == 1;
        public static bool IsVisible(AchievementDef def) => !def.arcadeOnly || ArcadeMode.IsUnlocked;

        public static string TitleOf(AchievementDef def) =>
            def.secret && !IsUnlocked(def.id) ? SecretTitle : def.title;
        public static string DescriptionOf(AchievementDef def) =>
            def.secret && !IsUnlocked(def.id) ? SecretDescription : def.description;

        public static int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (AchievementDef def in All)
                    if (IsVisible(def)) count++;
                return count;
            }
        }

        public static int UnlockedVisibleCount
        {
            get
            {
                int count = 0;
                foreach (AchievementDef def in All)
                    if (IsVisible(def) && IsUnlocked(def.id)) count++;
                return count;
            }
        }

        // Выдаёт достижение. Аркадные до открытия аркады не выдаются.
        // toastInMenu: плашка не сейчас, а при следующем заходе в главное меню
        public static void Award(AchievementId id, bool toastInMenu = false)
        {
            AchievementDef def = All[(int)id];
            if (IsUnlocked(id) || !IsVisible(def)) return;
            var game = Object.FindAnyObjectByType<SpinvGameManager>();
            if (game != null && game.IsTutorial) return; // обучение ничего не даёт

            PlayerPrefs.SetInt(Key(id), 1);
            if (toastInMenu)
            {
                string pending = PlayerPrefs.GetString(PendingKey, "");
                PlayerPrefs.SetString(PendingKey, pending.Length == 0 ? def.title : pending + "|" + def.title);
            }
            PlayerPrefs.Save();

            if (toastInMenu) return;
            var view = Object.FindAnyObjectByType<AchievementToast>();
            if (view != null) view.Show(def.title);
        }

        // Полный сброс: забыть все достижения и отложенные плашки
    public static void DeleteKeys()
    {
        foreach (AchievementDef def in All)
            PlayerPrefs.DeleteKey(Key(def.id));
        PlayerPrefs.DeleteKey(PendingKey);
    }

    // Показывает плашки, отложенные до главного меню
        public static void ShowPendingToasts(AchievementToast view)
        {
            string pending = PlayerPrefs.GetString(PendingKey, "");
            if (pending.Length == 0) return;

            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
            foreach (string title in pending.Split('|'))
                view.Show(title);
        }

    }
}
