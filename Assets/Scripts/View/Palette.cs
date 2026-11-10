using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>
    /// Colors of the dark magic-forest look from docs/prototype/lines-prototype.html (GDD → Art).
    /// Layered UI parts (a fill over its border) use nearly opaque colors so the border doesn't show through.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Screen = Hex(0x10202C);

        // Board: dark purple glass with a violet rim and glow; cells glow faintly from inside.
        public static readonly Color Board = Hex(0x171633, 0.95f);
        public static readonly Color BoardRim = Hex(0xBE96FF, 0.38f);
        public static readonly Color BoardGlow = Hex(0x966EFF, 0.12f);
        public static readonly Color Cell = Hex(0xFFFFFF, 0.025f);
        public static readonly Color CellLight = Hex(0xAADCFF, 0.06f);

        // Text.
        public static readonly Color Text = Hex(0xF6EEDB);
        public static readonly Color SoftText = Hex(0xA9BCCD);
        public static readonly Color Line = Hex(0xFFE9BE, 0.3f);
        public static readonly Color Focus = Hex(0xFFD56B);
        public static readonly Color Gain = Hex(0x9CF5B0);

        // Buttons and panels: dark glass with a warm outline; main buttons are glowing gold.
        public static readonly Color Glass = Hex(0x1E1640, 0.9f);
        public static readonly Color Gold = Hex(0xFFC45E);
        public static readonly Color GoldText = Hex(0x3A2208);
        public static readonly Color GoldGlow = Hex(0xFFBE5A, 0.13f);
        public static readonly Color DisabledFill = Hex(0x2A2448, 0.9f);
        public static readonly Color DisabledRim = Hex(0xFFFFFF, 0.15f);

        // Pop-ups.
        public static readonly Color Dim = Hex(0x050712, 0.74f);
        public static readonly Color Card = Hex(0x1C1A36);
        public static readonly Color CardGlow = Hex(0xA078FF, 0.05f);
        public static readonly Color Toast = Hex(0x0E0A1E, 0.9f);

        // Panels that let the forest show through (leaderboard rows), and the How to play mini board's cells.
        public static readonly Color SeeThroughPanel = Hex(0x1A1736, 0.62f);
        public static readonly Color MiniCell = Hex(0x23263F);

        // The "Lines" title: a gold gradient with a warm glow.
        public static readonly Color TitleTop = Hex(0xFFF2C2);
        public static readonly Color TitleBottom = Hex(0xFF9A3C);
        public static readonly Color TitleGlow = Hex(0xFFBE5A, 0.14f);

        /// <summary>Base, light (highlight side) and dark (shadow side) shade of a ball color (the fallback balls).</summary>
        public static (Color basis, Color light, Color dark) Ball(BallColor color)
        {
            switch (color)
            {
                case BallColor.Red: return (Hex(0xE5484D), Hex(0xFF9497), Hex(0xA3222A));
                case BallColor.Orange: return (Hex(0xF2851F), Hex(0xFFBF7A), Hex(0xAD5310));
                case BallColor.Yellow: return (Hex(0xF2C418), Hex(0xFFE782), Hex(0xA98400));
                case BallColor.Green: return (Hex(0x3AA757), Hex(0x86DC9B), Hex(0x1D6A32));
                case BallColor.Cyan: return (Hex(0x1FB6CC), Hex(0x85E6F2), Hex(0x0B7686));
                case BallColor.Blue: return (Hex(0x3A6EE0), Hex(0x93B2FF), Hex(0x1D3F98));
                case BallColor.Purple: return (Hex(0x8B4FD1), Hex(0xC7A3F3), Hex(0x572893));
                default: return (Color.clear, Color.clear, Color.clear);
            }
        }

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
