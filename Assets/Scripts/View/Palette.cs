using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>Colors from docs/prototype/lines-prototype.html (the game screen's single light theme).</summary>
    public static class Palette
    {
        public static readonly Color Screen = Hex(0xF3EFE7);
        public static readonly Color Board = Hex(0xDDD5C6);
        public static readonly Color Cell = Hex(0xF8F5EF);
        public static readonly Color Text = Hex(0x3A342C);
        public static readonly Color SoftText = Hex(0x8A7F70);
        public static readonly Color Line = Hex(0xD9D0C0);

        /// <summary>Base, light (highlight side) and dark (shadow side) shade of a ball color.</summary>
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

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
