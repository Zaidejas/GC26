using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Colors, font sizes and timings ("Mediterranean Coast", GDD 5.2 and 5.3). Artists tweak the asset
    /// Art/gc26_2048_nastya_Theme.asset; the defaults below are the GDD starting palette.
    /// </summary>
    [CreateAssetMenu(fileName = "gc26_2048_nastya_Theme", menuName = "GC26/Colours (nastya)/Theme")]
    public sealed class Theme2048 : ScriptableObject
    {
        [Serializable]
        public struct TileStyle
        {
            public int value;
            public Color tile;
            public Color text;
            [Range(0f, 1f)] public float glow;

            public TileStyle(int value, uint tileHex, uint textHex, float glow)
            {
                this.value = value;
                tile = Hex(tileHex);
                text = Hex(textHex);
                this.glow = glow;
            }
        }

        [Header("Board")]
        public Color background = Hex(0x231C20);
        public Color board = Hex(0x31272D);
        public Color emptyCell = Hex(0x42353C);

        [Header("HUD")]
        public Color hudText = Hex(0xFFF9F2);
        public Color hudMutedText = Hex(0xC28A8E);
        public Color button = Hex(0x933B5B);

        [Header("Splash and menu title gradient, left to right")]
        public Color titleStart = Hex(0x933B5B);  // Amaranth
        public Color titleMiddle = Hex(0xB5728A); // Thulian Pink
        public Color titleEnd = Hex(0xE3D6BF);    // Chalk

        [Header("Tiles (ascending values; larger values use the last entry)")]
        public List<TileStyle> tiles = DefaultTiles();

        [Header("Font size at a tile size of 220 px, by digit count")]
        public float fontSize1To2 = 110f;
        public float fontSize3 = 92f;
        public float fontSize4 = 72f;
        public float fontSize5Plus = 56f;

        [Header("Animation, seconds")]
        public float slide = 0.10f;
        public float pop = 0.12f;
        public float spawn = 0.12f;
        public float bump = 0.08f;
        public float scorePopup = 0.6f;

        public const float ReferenceTileSize = 220f;

        [NonSerialized] Dictionary<int, string> _valueText;

        /// <summary>Style for a tile value: the exact entry, or the last one for larger values.</summary>
        public TileStyle StyleFor(int value)
        {
            if (tiles == null || tiles.Count == 0) return new TileStyle(value, 0xFFFFFF, 0x141A26, 0f);
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i].value >= value) return tiles[i];
            return tiles[tiles.Count - 1];
        }

        /// <summary>The title gradient at t = 0 (left) .. 1 (right).</summary>
        public Color TitleColor(float t) => t < 0.5f
            ? Color.Lerp(titleStart, titleMiddle, t * 2f)
            : Color.Lerp(titleMiddle, titleEnd, t * 2f - 1f);

        public float FontSizeFor(int value, float tileSize)
        {
            float size = value < 100 ? fontSize1To2 : value < 1000 ? fontSize3 : value < 10000 ? fontSize4 : fontSize5Plus;
            return size * tileSize / ReferenceTileSize;
        }

        /// <summary>Cached number strings, so drawing a tile does not allocate (TDD 11).</summary>
        public string TextFor(int value)
        {
            _valueText ??= new Dictionary<int, string>(20);
            if (!_valueText.TryGetValue(value, out var s))
            {
                s = value.ToString();
                _valueText[value] = s;
            }
            return s;
        }

        public static List<TileStyle> DefaultTiles() => new List<TileStyle>
        {
            new TileStyle(2, 0xE3D6BF, 0x231C20, 0.0f),
            new TileStyle(4, 0xAABAAE, 0x1D2420, 0.0f),
            new TileStyle(8, 0x9F9679, 0xFFF9F2, 0.0f),
            new TileStyle(16, 0x848C7A, 0xFFF9F2, 0.0f),
            new TileStyle(32, 0xC28A8E, 0x231C20, 0.0f),
            new TileStyle(64, 0xB5728A, 0xFFF9F2, 0.0f),
            new TileStyle(128, 0xA45372, 0xFFF9F2, 0.25f),
            new TileStyle(256, 0x933B5B, 0xFFF9F2, 0.35f),
            new TileStyle(512, 0xAC3662, 0xFFF9F2, 0.45f),
            new TileStyle(1024, 0xC44569, 0xFFF9F2, 0.6f),
            new TileStyle(2048, 0xE3D6BF, 0x933B5B, 0.8f),
            new TileStyle(4096, 0xFFF9F2, 0x933B5B, 0.8f),
        };

        /// <summary>0xRRGGBB → Color (pure math, safe in field initializers).</summary>
        public static Color Hex(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
