using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Colors, font sizes and timings ("Night glass", GDD 5.2 and 5.3). Artists tweak the asset
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
        public Color background = Hex(0x141A26);
        public Color board = Hex(0x1F2738);
        public Color emptyCell = Hex(0x2A3448);

        [Header("HUD")]
        public Color hudText = Hex(0xE8F0FF);
        public Color hudMutedText = Hex(0x8FA3C4);
        public Color button = Hex(0x3A5A80);

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
            new TileStyle(2, 0x3A5A80, 0xE8F0FF, 0f),
            new TileStyle(4, 0x2F7A8C, 0xE8F0FF, 0f),
            new TileStyle(8, 0x2E9C7A, 0xFFFFFF, 0f),
            new TileStyle(16, 0x6BB34A, 0xFFFFFF, 0f),
            new TileStyle(32, 0xC9B23A, 0x1A1A1A, 0f),
            new TileStyle(64, 0xE08A2E, 0xFFFFFF, 0f),
            new TileStyle(128, 0xE0563A, 0xFFFFFF, 0.25f),
            new TileStyle(256, 0xD63A6E, 0xFFFFFF, 0.35f),
            new TileStyle(512, 0xA83AC9, 0xFFFFFF, 0.45f),
            new TileStyle(1024, 0x6A4AE0, 0xFFFFFF, 0.6f),
            new TileStyle(2048, 0xF5D76E, 0x1A1A1A, 0.8f),
            new TileStyle(4096, 0xFFFFFF, 0x141A26, 0.8f),
        };

        /// <summary>0xRRGGBB → Color (pure math, safe in field initializers).</summary>
        public static Color Hex(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
