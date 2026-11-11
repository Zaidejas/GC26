using UnityEngine;

namespace BlockDrop.View
{
    public enum StatSlot { Score, Second, Level, Lines }

    /// <summary>
    /// Where everything around the well goes. A wide window keeps the side panels; a tall one,
    /// like a phone held upright, puts hold, stats, bot and the queue in a band above the well.
    /// All positions are world units; the well itself always sits at the origin.
    /// </summary>
    public static class HudLayout
    {
        const float SideX = 6.2f;
        const float ColumnWidth = 3.8f;

        public static bool Portrait { get; private set; }

        /// <summary>Re-reads the window shape. Returns true when the layout switched.</summary>
        public static bool Refresh(int width, int height)
        {
            bool portrait = height > width * 1.05f;
            bool changed = portrait != Portrait;
            Portrait = portrait;
            return changed;
        }

        static float LeftX { get { return BoardView.LeftPanelX; } }
        static float RightX { get { return BoardView.RightPanelX; } }

        // --- camera ---------------------------------------------------------------------------

        /// <summary>Half the visible height, big enough that the whole layout fits the window.</summary>
        public static float OrthoSize(float aspect)
        {
            return Portrait ? Mathf.Max(15f, 8.4f / aspect) : Mathf.Max(11f, 11f / aspect);
        }

        /// <summary>During a game a tall window looks a little higher, to fit the band above the well.</summary>
        public static float GameCameraY { get { return Portrait ? 4.5f : 0f; } }

        // --- hold, queue and their panels -----------------------------------------------------

        public static Vector2 HoldCenter { get { return Portrait ? new Vector2(-SideX, 16.6f) : new Vector2(LeftX, 6.2f); } }
        public static Vector2 HoldLabel { get { return Portrait ? new Vector2(-SideX, 18.45f) : new Vector2(LeftX, 8f); } }
        public static Vector2 HoldPanelCenter { get { return Portrait ? new Vector2(-SideX, 17.05f) : new Vector2(LeftX, 6.2f); } }
        public static Vector2 HoldPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 4f) : new Vector2(5f, 4.6f); } }

        public static Vector2 NextLabel { get { return Portrait ? new Vector2(SideX, 18.45f) : new Vector2(RightX, 8f); } }
        public static Vector2 NextCenter(int slot)
        {
            return Portrait ? new Vector2(SideX, 16.6f - slot * 2.3f) : new Vector2(RightX, 6.2f - slot * 3f);
        }
        public static Vector2 NextPanelCenter { get { return Portrait ? new Vector2(SideX, 14.7f) : new Vector2(RightX, 3.2f); } }
        public static Vector2 NextPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 8.7f) : new Vector2(5f, 10.6f); } }

        public static Vector2 StatsPanelCenter { get { return Portrait ? new Vector2(-SideX, 12.6f) : new Vector2(LeftX, -1.05f); } }
        public static Vector2 StatsPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 4.3f) : new Vector2(5f, 9.1f); } }

        // --- numbers --------------------------------------------------------------------------

        /// <summary>Centre of one stat. Its caption sits above this point and its value below.</summary>
        public static Vector2 Stat(StatSlot slot)
        {
            if (!Portrait)
            {
                switch (slot)
                {
                    case StatSlot.Score: return new Vector2(LeftX, 2.2f);
                    case StatSlot.Second: return new Vector2(LeftX, 0f);
                    case StatSlot.Level: return new Vector2(LeftX, -2.2f);
                    default: return new Vector2(LeftX, -4.4f);
                }
            }
            switch (slot)
            {
                case StatSlot.Score: return new Vector2(-SideX, 13.95f);
                case StatSlot.Second: return new Vector2(0f, 18.45f);
                case StatSlot.Level: return new Vector2(-SideX, 12.65f);
                default: return new Vector2(-SideX, 11.35f);
            }
        }

        /// <summary>The tall layout packs three stats into a short panel, so they are drawn smaller.</summary>
        public static float StatScale(StatSlot slot)
        {
            return Portrait && slot != StatSlot.Second ? 0.72f : 1f;
        }

        public static Vector2 ComboLine { get { return Portrait ? new Vector2(0f, 11.35f) : new Vector2(LeftX, -6.6f); } }

        public static Vector2 LegendIcon(int line)
        {
            return Portrait ? new Vector2(-2.2f, 16.2f - line * 1.15f) : new Vector2(RightX - 2.05f, -5.0f - line * 1.05f);
        }

        public static float LegendTextX { get { return Portrait ? 0.75f : RightX + 0.45f; } }

        // --- the bot --------------------------------------------------------------------------

        public static Vector2 BotPanelCenter { get { return Portrait ? new Vector2(0f, 15.75f) : new Vector2(RightX, -6.5f); } }
        public static Vector2 BotPanelSize { get { return Portrait ? new Vector2(8f, 6.6f) : new Vector2(5f, 7.8f); } }
        public static Vector2 BotCenter { get { return Portrait ? new Vector2(0f, 15.6f) : new Vector2(RightX, -6.3f); } }
        public static float BotNameY { get { return Portrait ? 18.6f : -3.12f; } }
        public static float BotBarY { get { return Portrait ? 17.95f : -3.85f; } }
        /// <summary>Where the coins fly to: the coin counter.</summary>
        public static Vector2 CoinCounter { get { return Portrait ? new Vector2(0f, 13.15f) : new Vector2(LeftX, -0.25f); } }

        // --- menu -----------------------------------------------------------------------------

        public static float MenuTitleY { get { return Portrait ? 10.6f : 7.3f; } }
        public static float MenuTopY { get { return Portrait ? 6.4f : 4.3f; } }
        public static Vector2 MenuTable(int index)
        {
            return Portrait ? new Vector2(0f, -4.3f - index * 5.1f) : new Vector2(index == 0 ? -4.65f : 4.65f, -6.9f);
        }
        public static float MenuHintY { get { return Portrait ? -13.3f : -9.95f; } }
        public static Vector2 MenuCoins { get { return Portrait ? new Vector2(0f, -14.2f) : new Vector2(9.6f, -9.95f); } }
    }
}
