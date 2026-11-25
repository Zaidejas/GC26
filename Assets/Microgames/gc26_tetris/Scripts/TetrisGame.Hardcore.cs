using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;

namespace GC26.Tetris
{
    /// <summary>
    /// Hardcore on screen: pieces fall fast from the first level (the rules handle that), and every clear pays
    /// coins, a line times the combo. The coins go straight into the same wallet the shop uses.
    /// </summary>
    public sealed partial class TetrisGame
    {
        void PayHardcoreCoins(LineClear clear, float worldY)
        {
            int coins = TetrisMatch.HardcoreCoins(clear.Rows.Count, clear.Combo);
            if (coins <= 0)
                return;
            hardcoreCoins += coins;
            coinsTotal += coins;
            CountStat(PlayStat.Coins, coins);
            coinKick = 1f;
            Debug.Log("[BlockDrop] Hardcore coins +" + coins + " (lines " + clear.Rows.Count + ", combo " + clear.Combo + "), this game " + hardcoreCoins + ", total " + coinsTotal);
            if (!autoplay && !noRecords)
            {
                PlayerPrefs.SetInt(recordsPrefix + CoinsName, coinsTotal);
                PlayerPrefs.Save();
            }
            gameAudio.Play("coin", 0.5f + Mathf.Min(coins, 10) * 0.04f, 1f + Mathf.Min(clear.Combo, 6) * 0.04f);
            AddPopup(PopupSlot.Coins, "+" + coins + T(" мон.", " coins"), worldY - 1.2f, 0.75f + Mathf.Min(coins, 12) * 0.03f, 1f, 1.4f, Gold);
        }

        static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

        /// <summary>The coins earned this game, under the best score.</summary>
        void DrawHardcoreCoins(float unit)
        {
            Vector2 at = HudLayout.Stat(StatSlot.Second);
            float scale = HudLayout.StatScale(StatSlot.Second);
            Label(T("монеты +", "coins +") + hardcoreCoins, at.x, at.y - 0.95f * scale, unit, 0.42f * scale * (1f + coinKick * 0.3f), TextAnchor.MiddleCenter, Gold);
        }
    }
}
