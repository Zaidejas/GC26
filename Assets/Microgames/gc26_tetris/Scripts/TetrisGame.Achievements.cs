using System.Collections.Generic;
using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Achievements on screen: counting what happens in play, the banner when one unlocks (with its coins),
    /// and the list opened from the main menu. The rules live in TetrisAchievements.
    /// </summary>
    public sealed partial class TetrisGame
    {
        const string AchievementsName = "Achievements";
        const float ToastLife = 3f;

        TetrisAchievements achievements = new TetrisAchievements();
        readonly List<Achievement> toasts = new List<Achievement>();
        float toastAge;
        bool inAchievements;
        int achievementsIndex;
        readonly float[] achievementsGlow = { 1f };

        void LoadAchievements()
        {
            achievements = TetrisAchievements.Parse(PlayerPrefs.GetString(recordsPrefix + AchievementsName, ""));
            // Records set before achievements existed still count.
            Track(AchievementStat.Score, Mathf.Max(Best(marathonTable), Best(battleTable)));
        }

        void SaveAchievements()
        {
            if (autoplay || noRecords)
                return;
            PlayerPrefs.SetString(recordsPrefix + AchievementsName, achievements.Serialize());
            PlayerPrefs.SetInt(recordsPrefix + CoinsName, coinsTotal);
            PlayerPrefs.Save();
        }

        /// <summary>Reports one thing that happened. Unlocked achievements pay their coins and queue a banner.</summary>
        void Track(AchievementStat stat, long amount)
        {
            if (autoplay || noRecords)
                return;
            List<Achievement> reached = achievements.Report(stat, amount);
            if (reached.Count == 0)
                return;
            foreach (Achievement achievement in reached)
            {
                coinsTotal += achievement.Reward;
                CountStat(PlayStat.Coins, achievement.Reward);
                toasts.Add(achievement);
                Debug.Log("[BlockDrop] Achievement " + achievement.Key + " +" + achievement.Reward + " coins, total=" + coinsTotal);
            }
            if (gameAudio != null)
                gameAudio.Play("levelup", 0.6f, 1.15f);
            SaveAchievements();
        }

        void UpdateAchievementToasts()
        {
            if (toasts.Count == 0)
                return;
            toastAge += Time.unscaledDeltaTime;
            if (toastAge >= ToastLife)
            {
                toasts.RemoveAt(0);
                toastAge = 0f;
            }
        }

        // --- the list -------------------------------------------------------------------------

        void OpenAchievements()
        {
            Debug.Log("[BlockDrop] Achievements opened: " + achievements.Serialize());
            inAchievements = true;
            achievementsIndex = 0;
            gameAudio.Play("hold");
        }

        void CloseAchievements()
        {
            Debug.Log("[BlockDrop] Achievements closed");
            inAchievements = false;
            gameAudio.Play("hold", 0.6f);
        }

        void UpdateAchievementsScreen()
        {
            menuClock += Time.deltaTime;
            if (Pressed(Key.Escape) || Pressed(Key.Backspace))
            {
                CloseAchievements();
                return;
            }
            if (Pressed(Key.L))
                SetLanguage(!english);
            if (HandleButtons(ref achievementsIndex, 1, HudLayout.AchievementsBackY, 1f, 6f, 1.1f))
                CloseAchievements();
        }

        void DrawAchievementsScreen(float unit)
        {
            FillWorld(0f, 0f, 200f, 200f, new Color(0.07f, 0.055f, 0.1f, 1f));
            DrawMenuBackground();

            Label(T("ДОСТИЖЕНИЯ", "ACHIEVEMENTS"), 0f, HudLayout.AchievementsTitleY, unit, 1.15f, TextAnchor.MiddleCenter, view.AccentColor);
            Label(achievements.UnlockedCount + " / " + TetrisAchievements.All.Length + T("   ·   монеты ", "   ·   coins ") + coinsTotal.ToString("N0"),
                0f, HudLayout.AchievementsCountY, unit, 0.5f, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.25f));

            for (int i = 0; i < TetrisAchievements.All.Length; i++)
                DrawAchievementRow(TetrisAchievements.All[i], HudLayout.AchievementsTopY - i * HudLayout.AchievementsStepY, unit);

            DrawButton(T("Назад", "Back"), null, HudLayout.AchievementsBackY, 6f, 1.1f, achievementsGlow[0], 0, unit);
            Label(T("Esc назад", "Esc back"), 0f, HudLayout.MenuHintY, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
        }

        void DrawAchievementRow(Achievement achievement, float y, float unit)
        {
            float width = HudLayout.AchievementsWidth, height = HudLayout.AchievementsRowHeight;
            bool done = achievements.IsUnlocked(achievement);
            Color gold = new Color(1f, 0.8f, 0.25f);
            RoundedWorld(0f, y, width, height, done ? new Color(0.2f, 0.17f, 0.12f, 1f) : new Color(0.12f, 0.1f, 0.18f, 1f), 0.3f, 0f, unit);
            if (done)
                RoundedWorld(0f, y, width, height, new Color(gold.r, gold.g, gold.b, 0.55f), 0.3f, 0.05f, unit);

            float left = -width / 2f;
            DrawTile(left + 0.6f, y, 0.55f, done ? gold : new Color(0.32f, 0.29f, 0.4f));

            // Left-aligned text: Label centres a 26-unit box on x, so x is the text's left edge plus 13.
            bool tall = HudLayout.Portrait;
            float textLeft = left + 1.15f + 13f;
            Label(AchievementName(achievement), textLeft, y + height * 0.18f, unit, tall ? 0.42f : 0.4f, TextAnchor.MiddleLeft, done ? Bright : Muted);
            Label(AchievementHint(achievement), textLeft, y - height * 0.24f, unit, tall ? 0.28f : 0.27f, TextAnchor.MiddleLeft, Muted);

            string right;
            Color rightColor;
            if (done)
            {
                right = "+" + achievement.Reward + T(" мон.", " coins");
                rightColor = new Color(0.55f, 0.9f, 0.5f);
            }
            else
            {
                long value = System.Math.Min(achievements.Value(achievement.Stat), achievement.Target);
                right = value.ToString("N0") + " / " + achievement.Target.ToString("N0") + "\n" + T("награда ", "reward ") + achievement.Reward;
                rightColor = gold;
            }
            // Right-aligned the same way: the box's right edge sits at x + 13.
            Label(right, width / 2f - 0.4f - 13f, y, unit, done ? 0.36f : 0.28f, TextAnchor.MiddleRight, rightColor);
        }

        /// <summary>The banner at the top of the screen while an achievement has just been unlocked.</summary>
        void DrawAchievementToast(float unit)
        {
            if (toasts.Count == 0)
                return;
            Achievement achievement = toasts[0];
            float fadeIn = Mathf.Clamp01(toastAge / 0.25f), fadeOut = Mathf.Clamp01((ToastLife - toastAge) / 0.4f);
            float alpha = Mathf.Min(fadeIn, fadeOut);
            float top = mainCamera.transform.position.y + mainCamera.orthographicSize;
            float y = top - 1.35f + (1f - fadeIn) * 0.6f;
            float width = Mathf.Min(12f, mainCamera.orthographicSize * mainCamera.aspect * 2f - 0.6f);
            Color gold = new Color(1f, 0.8f, 0.25f, alpha);

            RoundedWorld(0f, y - 0.08f, width, 1.9f, new Color(0f, 0f, 0f, 0.45f * alpha), 0.4f, 0f, unit);
            RoundedWorld(0f, y, width, 1.9f, new Color(0.16f, 0.12f, 0.08f, 0.96f * alpha), 0.4f, 0f, unit);
            RoundedWorld(0f, y, width, 1.9f, gold, 0.4f, 0.07f, unit);
            DrawTile(-width / 2f + 0.8f, y, 0.75f, gold);
            Label(T("ДОСТИЖЕНИЕ!", "ACHIEVEMENT!") + "   +" + achievement.Reward + T(" мон.", " coins"), 0.35f, y + 0.42f, unit, 0.36f, TextAnchor.MiddleCenter, gold);
            Label(AchievementName(achievement), 0.35f, y - 0.25f, unit, 0.6f, TextAnchor.MiddleCenter, new Color(Bright.r, Bright.g, Bright.b, alpha));
        }

        // --- texts ----------------------------------------------------------------------------

        string AchievementName(Achievement achievement)
        {
            switch (achievement.Key)
            {
                case "first_line": return T("Первая линия", "First line");
                case "four_lines": return T("Четыре сразу", "Four at once");
                case "combo5": return T("Комбо ×5", "Combo ×5");
                case "combo10": return T("Мастер комбо", "Combo master");
                case "lines100": return T("Сотня линий", "A hundred lines");
                case "score10k": return T("10 000 очков", "10,000 points");
                case "score50k": return T("50 000 очков", "50,000 points");
                case "level10": return T("Десятый уровень", "Level ten");
                case "bombs10": return T("Подрывник", "Demolisher");
                case "lightnings10": return T("Громовержец", "Thunderer");
                case "first_bot": return T("Первая победа", "First win");
                case "bots3": return T("Гроза ботов", "Bot breaker");
                default: return T("Щит из линий", "Line shield");
            }
        }

        string AchievementHint(Achievement achievement)
        {
            switch (achievement.Key)
            {
                case "first_line": return T("очисти любую линию", "clear any line");
                case "four_lines": return T("очисти 4 линии одной фигурой", "clear 4 lines with one piece");
                case "combo5": return T("5 очисток подряд", "5 clears in a row");
                case "combo10": return T("10 очисток подряд", "10 clears in a row");
                case "lines100": return T("100 линий за все игры", "100 lines over all games");
                case "score10k": return T("набери за одну игру", "in one game");
                case "score50k": return T("набери за одну игру", "in one game");
                case "level10": return T("дойди до 10 уровня", "reach level 10");
                case "bombs10": return T("взорви 10 бомб", "set off 10 bombs");
                case "lightnings10": return T("10 ударов молнии", "10 lightning strikes");
                case "first_bot": return T("победи бота", "beat a bot");
                case "bots3": return T("3 бота за один бой", "3 bots in one battle");
                default: return T("отбей 5 атак бота", "block 5 bot attacks");
            }
        }
    }
}
