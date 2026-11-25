using GC26.Tetris.Logic;
using UnityEngine;

namespace GC26.Tetris.View
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

        // The tall layout: the band above the well runs from the well's top edge up BandHeight units, and the
        // floor sits just under the well. On a taller screen the band stretches upward to fill what is left.
        const float WellTop = 10f;
        const float BandHeight = 9.3f;
        const float WellFloor = -10.5f;
        const float BottomBreathing = 1f;

        // Shares of the screen height under a camera cut-out or system bars, top and bottom.
        static float insetTop, insetBottom;
        static float currentOrtho = 15f;
        static bool stretchChanged;

        /// <summary>How much taller than normal the band above the well is drawn (1 on a screen with no room to spare).</summary>
        public static float BandStretch { get; private set; } = 1f;

        /// <summary>Text and badges in the band grow with it, a little more gently.</summary>
        public static float ItemScale { get { return Portrait ? Mathf.Min(BandStretch, 1.35f) : 1f; } }

        /// <summary>Hold and queue pieces grow too, but a four-cell I must still fit its panel.</summary>
        public static float PieceScale { get { return Portrait ? Mathf.Min(BandStretch, 1.18f) : 1f; } }

        /// <summary>
        /// The middle of the band's centre column (best score, then the bomb and lightning legend): halfway between
        /// the band's top and the combo line, so the group sits in the middle however far the band stretches.
        /// </summary>
        static float CenterColumnMid
        {
            get { return (B(19f) + B(11.35f) + 0.6f) / 2f; }
        }

        /// <summary>A height in the band, measured from the well's top edge and stretched with it.</summary>
        static float B(float y)
        {
            return WellTop + (y - WellTop) * BandStretch;
        }

        /// <summary>Re-reads the window shape. Returns true when the layout switched.</summary>
        public static bool Refresh(int width, int height)
        {
            // UnityEngine.Device follows the editor's Device Simulator too, cut-out included.
            Rect safe = UnityEngine.Device.Screen.safeArea;
            if (height > 0 && safe.height > 0f)
            {
                insetTop = Mathf.Clamp01((height - safe.yMax) / height);
                insetBottom = Mathf.Clamp01(safe.yMin / height);
            }
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
            currentOrtho = Portrait ? Mathf.Max(15f, 8.4f / aspect) : Mathf.Max(11f, 11f / aspect);
            float stretch = 1f;
            if (Portrait)
            {
                // The width sets the zoom, so a tall phone has height to spare: give it to the band.
                float free = currentOrtho * 2f * (1f - insetTop - insetBottom);
                float spare = free - (WellTop + BandHeight - WellFloor) - BottomBreathing;
                stretch = Mathf.Clamp(1f + spare / BandHeight, 1f, 1.8f);
            }
            if (Mathf.Abs(stretch - BandStretch) > 0.005f)
            {
                BandStretch = stretch;
                stretchChanged = true;
            }
            return currentOrtho;
        }

        /// <summary>True once after the band's stretch changed, so the panels can be placed again.</summary>
        public static bool TakeStretchChange()
        {
            bool changed = stretchChanged;
            stretchChanged = false;
            return changed;
        }

        /// <summary>
        /// During a game the tall layout centres the band and the well between the cut-out and the bottom bar.
        /// </summary>
        public static float GameCameraY
        {
            get
            {
                if (!Portrait)
                    return 0f;
                float span = currentOrtho * 2f;
                float top = WellTop + BandHeight * BandStretch + insetTop * span;
                float bottom = WellFloor - insetBottom * span;
                return (top + bottom) / 2f;
            }
        }

        // --- hold, queue and their panels -----------------------------------------------------

        public static Vector2 HoldCenter { get { return Portrait ? new Vector2(-SideX, B(16.6f)) : new Vector2(LeftX, 6.2f); } }
        public static Vector2 HoldLabel { get { return Portrait ? new Vector2(-SideX, B(18.45f)) : new Vector2(LeftX, 8f); } }
        public static Vector2 HoldPanelCenter { get { return Portrait ? new Vector2(-SideX, B(17.05f)) : new Vector2(LeftX, 6.2f); } }
        public static Vector2 HoldPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 4f * BandStretch) : new Vector2(5f, 4.6f); } }

        /// <summary>The clickable pause button: top right in the wide layout, beside the well in the tall one.</summary>
        public static Vector2 PauseButtonCenter { get { return Portrait ? new Vector2(Board.Width / 2f + 1.05f, 9.1f) : new Vector2(RightX, 10.2f); } }
        public static Vector2 PauseButtonSize { get { return Portrait ? new Vector2(1.4f, 1.5f) : new Vector2(5f, 1.2f); } }

        // --- strips beside the well (tall layout) ---------------------------------------------

        static float StripX { get { return Board.Width / 2f + 1.05f; } }
        const float StripWidth = 1.4f;
        const float StripBottom = -9.6f;

        /// <summary>Left of the well: the level bar, with the level number above it.</summary>
        public static Rect LevelStrip
        {
            get { return Rect.MinMaxRect(-StripX - StripWidth / 2f, StripBottom, -StripX + StripWidth / 2f, 8.4f); }
        }

        /// <summary>Right of the well, under the pause button: the combo bar, with the multiplier above it.</summary>
        public static Rect ComboStrip
        {
            get { return Rect.MinMaxRect(StripX - StripWidth / 2f, StripBottom, StripX + StripWidth / 2f, 7.4f); }
        }

        public static Vector2 NextLabel { get { return Portrait ? new Vector2(SideX, B(18.45f)) : new Vector2(RightX, 8f); } }
        public static Vector2 NextCenter(int slot)
        {
            return Portrait ? new Vector2(SideX, B(16.6f - slot * 2.3f)) : new Vector2(RightX, 6.2f - slot * 3f);
        }
        public static Vector2 NextPanelCenter { get { return Portrait ? new Vector2(SideX, B(14.7f)) : new Vector2(RightX, 3.2f); } }
        public static Vector2 NextPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 8.7f * BandStretch) : new Vector2(5f, 10.6f); } }

        public static Vector2 StatsPanelCenter { get { return Portrait ? new Vector2(-SideX, B(12.6f)) : new Vector2(LeftX, -1.05f); } }
        public static Vector2 StatsPanelSize { get { return Portrait ? new Vector2(ColumnWidth, 4.3f * BandStretch) : new Vector2(5f, 9.1f); } }

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
                case StatSlot.Score: return new Vector2(-SideX, B(13.95f));
                case StatSlot.Second: return new Vector2(0f, CenterColumnMid + 1.25f * ItemScale);
                case StatSlot.Level: return new Vector2(-SideX, B(12.65f));
                default: return new Vector2(-SideX, B(11.35f));
            }
        }

        /// <summary>The tall layout packs three stats into a short panel, so they are drawn smaller.</summary>
        public static float StatScale(StatSlot slot)
        {
            return Portrait ? (slot != StatSlot.Second ? 0.72f : 1f) * ItemScale : 1f;
        }

        public static Vector2 ComboLine { get { return Portrait ? new Vector2(0f, B(11.35f)) : new Vector2(LeftX, -6.6f); } }

        public static Vector2 LegendIcon(int line)
        {
            return Portrait ? new Vector2(-2.2f, CenterColumnMid - (0.85f + line * 1.15f) * ItemScale) : new Vector2(RightX - 2.05f, -5.0f - line * 1.05f);
        }

        public static float LegendTextX { get { return Portrait ? 0.75f : RightX + 0.45f; } }

        // --- the bot --------------------------------------------------------------------------

        public static Vector2 BotPanelCenter { get { return Portrait ? new Vector2(0f, B(15.75f)) : new Vector2(RightX, -6.5f); } }
        public static Vector2 BotPanelSize { get { return Portrait ? new Vector2(8f, 6.6f * BandStretch) : new Vector2(5f, 7.8f); } }
        public static Vector2 BotCenter { get { return Portrait ? new Vector2(0f, B(15.6f)) : new Vector2(RightX, -6.3f); } }
        public static float BotNameY { get { return Portrait ? B(18.6f) : -3.12f; } }
        public static float BotBarY { get { return Portrait ? B(17.95f) : -3.85f; } }
        /// <summary>Where the coins fly to: the coin counter.</summary>
        public static Vector2 CoinCounter { get { return Portrait ? new Vector2(0f, B(13.15f)) : new Vector2(LeftX, -0.25f); } }

        // --- menu -----------------------------------------------------------------------------

        public static float MenuTitleY { get { return Portrait ? 10.6f : 7.3f; } }
        public static float MenuTopY { get { return Portrait ? 7.6f : 5.2f; } }
        public static Vector2 MenuTable(int index)
        {
            return Portrait ? new Vector2(0f, -4.8f - index * 5.1f) : new Vector2(index == 0 ? -4.65f : 4.65f, -7.1f);
        }
        public static float MenuHintY { get { return Portrait ? -13.3f : -9.95f; } }
        // --- shop -----------------------------------------------------------------------------

        public static float ShopTitleY { get { return Portrait ? 12.8f : 9.5f; } }
        public static float ShopCoinsY { get { return Portrait ? 11.3f : 8.1f; } }
        public static float ShopTopY { get { return Portrait ? 9.3f : 6.5f; } }
        public static float ShopStepY { get { return Portrait ? 1.75f : 1.62f; } }
        public static float ShopItemWidth { get { return Portrait ? 12.6f : 15f; } }
        public static float ShopItemHeight { get { return Portrait ? 1.5f : 1.38f; } }
        public static float ShopMessageY { get { return Portrait ? -5.3f : -7.2f; } }

        // --- achievements ---------------------------------------------------------------------

        public static float AchievementsTitleY { get { return Portrait ? 13.2f : 9.7f; } }
        public static float AchievementsCountY { get { return Portrait ? 11.9f : 8.6f; } }
        public static float AchievementsTopY { get { return Portrait ? 10.5f : 7.4f; } }
        public static float AchievementsStepY { get { return Portrait ? 1.62f : 1.17f; } }
        public static float AchievementsRowHeight { get { return Portrait ? 1.42f : 1.02f; } }
        public static float AchievementsWidth { get { return Portrait ? 12.8f : 16f; } }
        public static float AchievementsBackY { get { return Portrait ? -11.6f : -8.5f; } }

        // --- statistics -----------------------------------------------------------------------

        /// <summary>The wide layout shows the statistics in two columns, the tall one in one.</summary>
        public static int StatsColumns { get { return Portrait ? 1 : 2; } }
        public static float StatsColumnX(int column) { return Portrait ? 0f : (column == 0 ? -5.1f : 5.1f); }
        public static float StatsTopY { get { return Portrait ? 10.3f : 7.0f; } }
        public static float StatsStepY { get { return Portrait ? 1.3f : 1.58f; } }
        public static float StatsRowHeight { get { return Portrait ? 1.12f : 1.32f; } }
        public static float StatsRowWidth { get { return Portrait ? 12.8f : 9.7f; } }

        /// <summary>The RU | EN switch in the main menu's top right corner.</summary>
        public static Vector2 LanguageButtonCenter { get { return Portrait ? new Vector2(4.9f, 13.6f) : new Vector2(10.2f, 10.1f); } }
        public static Vector2 LanguageButtonSize { get { return new Vector2(3f, 1.05f); } }

        public static Vector2 MenuCoins { get { return Portrait ? new Vector2(0f, -14.2f) : new Vector2(9.6f, -9.95f); } }
    }
}
