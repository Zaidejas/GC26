using System;
using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>
    /// What the 7 ball colors look like on screen (GDD → Art): the 7 glowing mushrooms, the same in every
    /// game, exported from docs/prototype/lines-prototype.html; or the generated glossy balls, which are only
    /// the fallback when the mushroom images are missing.
    /// </summary>
    public sealed class PieceSet
    {
        public const int MushroomCount = 7;
        const string MushroomFolder = "Mushrooms/mushroom_";
        static readonly Sprite[] MushroomCache = new Sprite[MushroomCount];

        // Each mushroom's glow colour (the prototype's --c), for the burst particles. Index = color - 1.
        static readonly int[] MushroomColors = { 0xFF3B5C, 0xFF8A1F, 0xFFD23F, 0x7ED957, 0xFF4FC3, 0x3FA0FF, 0xA35CFF };

        public bool UsesImages { get; }

        /// <summary>Piece size compared to a cell (prototype: balls 80%, pictures 94%).</summary>
        public float SizeInCell => UsesImages ? 0.94f : 0.8f;

        /// <summary>Preview dot size compared to a cell (prototype: balls 30%, pictures 42%).</summary>
        public float DotSizeInCell => UsesImages ? 0.42f : 0.30f;

        PieceSet(bool usesImages) { UsesImages = usesImages; }

        public static PieceSet Balls() => new PieceSet(false);

        /// <summary>The 7 mushrooms; falls back to balls if the images are missing.</summary>
        public static PieceSet Mushrooms() => new PieceSet(MushroomsAvailable());

        public static bool MushroomsAvailable() => Mushroom(0) != null;

        public Sprite SpriteFor(BallColor color) =>
            UsesImages && color != BallColor.None ? Mushroom((int)color - 1) : SpriteFactory.Ball(color);

        /// <summary>The color of the particles when a piece of <paramref name="color"/> bursts.</summary>
        public Color ParticleColor(BallColor color) =>
            UsesImages && color != BallColor.None ? Palette.Hex(MushroomColors[(int)color - 1]) : Palette.Ball(color).basis;

        static Sprite Mushroom(int index)
        {
            if (index < 0 || index >= MushroomCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (MushroomCache[index] == null) MushroomCache[index] = Resources.Load<Sprite>(MushroomFolder + index);
            return MushroomCache[index];
        }
    }
}
