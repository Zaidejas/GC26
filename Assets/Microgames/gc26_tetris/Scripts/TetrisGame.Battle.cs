using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;

namespace GC26.Tetris
{
    /// <summary>The battle mode on screen: bot panel, damage numbers and coins.</summary>
    public sealed partial class TetrisGame
    {
        const int MaxCoinsPerDrop = 14;

        Battle battle;
        BattleView battleView;
        int coinsTotal;
        float shownCoins;
        float coinKick;
        float shownHpShare = 1f;

        void CreateBattleView()
        {
            battleView = new BattleView(transform, spriteMaterial, view.PlainSprite, view.PanelColor, view.Effects,
                HudLayout.BotCenter, HudLayout.CoinCounter, HudLayout.BotPanelCenter, HudLayout.BotPanelSize);
            battleView.CoinArrived += () =>
            {
                coinKick = 1f;
                gameAudio.Play("coin", 0.4f);
            };
        }

        void ResetBattle()
        {
            bool fighting = mode == GameMode.Battle;
            battle = fighting ? new Battle() : null;
            battleView.Show(fighting);
            view.SetLegendVisible(!fighting);
            shownCoins = coinsTotal;
            shownHpShare = 1f;
            coinKick = 0f;
        }

        void ApplyBattleHit(LineClear clear)
        {
            BattleHit hit = battle.Hit(clear.Rows.Count, clear.Combo, clear.Multiplier);
            coinsTotal += hit.Coins;
            game.AddBonusScore(hit.Points);
            Debug.Log("[BlockDrop] Bot hit damage=" + hit.Damage + " points=" + hit.Points + " score=" + game.Score + " coins=" + hit.Coins + " bot=" + hit.BotLevel +
                      " defeated=" + hit.Defeated + " hp=" + battle.Hp + "/" + battle.MaxHp + " total=" + coinsTotal);
            if (!autoplay && !noRecords)
            {
                PlayerPrefs.SetInt(recordsPrefix + CoinsName, coinsTotal);
                PlayerPrefs.Save();
            }

            battleView.DropCoins(Mathf.Min(hit.Coins, MaxCoinsPerDrop));
            string multiplier = clear.Multiplier > 1f ? "  ×" + MultiplierText(clear.Multiplier) : "";
            Vector2 bot = HudLayout.BotCenter;
            AddPopup(PopupSlot.Damage, "−" + hit.Damage + multiplier, bot.y + 1.05f, 0.8f, 0.9f, 0.45f,
                new Color(1f, 0.45f, 0.35f), bot.x);

            if (hit.Defeated)
            {
                gameAudio.Play("botdown");
                view.Effects.Shake(0.9f);
                battleView.BotDefeated(battle.Level);
                shownHpShare = 1f;
                AddPopup(PopupSlot.Defeat, "БОТ ПОВЕРЖЕН!  +" + (Battle.DefeatPointsPerLevel * hit.BotLevel).ToString("N0"), -2.2f, 1.15f, 1.6f, 0.6f, view.AccentColor);
            }
            else
            {
                gameAudio.Play("bothit", 0.8f);
                battleView.BotHit(0.5f + hit.Damage / 80f);
            }
        }

        void UpdateBattleLooks(float deltaTime)
        {
            if (battle == null)
                return;
            battleView.Tick(deltaTime);
            battleView.SetPanelColor(view.PanelColor);
            coinKick = Mathf.Max(0f, coinKick - deltaTime * 5f);
            shownCoins = Mathf.MoveTowards(shownCoins, coinsTotal, Mathf.Max(8f, (coinsTotal - shownCoins) * 3f) * deltaTime);
            shownHpShare = Mathf.MoveTowards(shownHpShare, battle.Hp / (float)battle.MaxHp, deltaTime * 1.5f);
        }

        void DrawBotPanel(float unit)
        {
            float x = HudLayout.BotCenter.x;
            Label("Бот " + battle.Level + ": " + BattleView.NameFor(battle.Level), x, HudLayout.BotNameY, unit, 0.44f, TextAnchor.MiddleCenter, Bright);

            const float BarWidth = 4.3f, BarHeight = 0.5f;
            float BarY = HudLayout.BotBarY;
            RoundedWorld(x, BarY, BarWidth, BarHeight, new Color(0.05f, 0.04f, 0.08f, 1f), 0.14f, 0f, unit);
            float fill = (BarWidth - 0.1f) * Mathf.Clamp01(shownHpShare);
            if (fill > 0.05f)
            {
                Color bar = Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.45f), shownHpShare);
                RoundedWorld(x - (BarWidth - 0.1f - fill) / 2f, BarY, fill, BarHeight - 0.1f, bar, 0.1f, 0f, unit);
            }
            Label(battle.Hp + " / " + battle.MaxHp, x, BarY, unit, 0.32f, TextAnchor.MiddleCenter, Bright);

            if (HudLayout.Portrait)
            {
                // The tall layout has no left column to spare, so coins and best score sit under the bot.
                int best = Mathf.Max(Best(battleTable), game.Score);
                Vector2 coins = HudLayout.CoinCounter;
                Label("монеты " + Mathf.RoundToInt(shownCoins).ToString("N0"), coins.x, coins.y, unit, 0.5f * (1f + coinKick * 0.25f), TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.25f));
                Label("рекорд " + best.ToString("N0") + "   ·   повержено " + battle.BotsDefeated, coins.x, coins.y - 0.62f, unit, 0.34f, TextAnchor.MiddleCenter, Muted);
                return;
            }
            Label("Повержено: " + battle.BotsDefeated, x, -8.75f, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
            Label("C запас   P пауза", x, -9.7f, unit, 0.34f, TextAnchor.MiddleCenter, Muted);
        }

        void RelayoutBattle()
        {
            battleView.Relayout(HudLayout.BotCenter, HudLayout.CoinCounter, HudLayout.BotPanelCenter, HudLayout.BotPanelSize);
        }
    }
}
