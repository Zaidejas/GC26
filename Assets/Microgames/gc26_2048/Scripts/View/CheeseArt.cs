using System.Collections.Generic;
using UnityEngine;

namespace GC26._2048
{
    /// <summary>
    /// Palette and sprites: item art from Resources/gc26_2048/, rounded boxes drawn at start.
    /// Textures made here are destroyed in <see cref="Dispose"/> so nothing outlives the scene.
    /// </summary>
    public class CheeseArt
    {
        public static readonly Color32 Page = new Color32(0x2f, 0x4a, 0x24, 0xff);
        public static readonly Color32 Sky = new Color32(0xcd, 0xea, 0xf3, 0xff);
        public static readonly Color32 Meadow = new Color32(0x93, 0xc8, 0x62, 0xff);
        public static readonly Color32 MeadowDeep = new Color32(0x5f, 0x9a, 0x3a, 0xff);
        public static readonly Color32 Wood = new Color32(0xc9, 0x8a, 0x4b, 0xff);
        public static readonly Color32 Plank = new Color32(0xef, 0xd3, 0xa4, 0xff);
        public static readonly Color32 Slot = new Color32(0xd9, 0xb3, 0x7c, 0xff);
        public static readonly Color32 Cheese = new Color32(0xff, 0xc9, 0x3c, 0xff);
        public static readonly Color32 CheeseDeep = new Color32(0xd9, 0x9a, 0x12, 0xff);
        public static readonly Color32 Ink = new Color32(0x3a, 0x2a, 0x1c, 0xff);
        public static readonly Color32 InkDim = new Color32(0x6e, 0x5a, 0x45, 0xff);
        public static readonly Color32 Panel = new Color32(0xff, 0xf9, 0xea, 0xff);
        public static readonly Color32 Warn = new Color32(0xd9, 0x48, 0x2b, 0xff);
        public static readonly Color32 Shade = new Color32(0x2b, 0x20, 0x14, 0x80);
        public static readonly Color32 Light = new Color32(0xf4, 0xfb, 0xe9, 0xff);

        static readonly Color32[] TileColors =
        {
            new Color32(0xff, 0xff, 0xff, 0xff),
            new Color32(0xe2, 0xf5, 0xcf, 0xff), // grass
            new Color32(0xff, 0xf1, 0xc2, 0xff), // hay
            new Color32(0xf4, 0xed, 0xe6, 0xff), // cow
            new Color32(0xe3, 0xe9, 0xef, 0xff), // milk can
            new Color32(0xe6, 0xf4, 0xfb, 0xff), // bottle
            new Color32(0xff, 0xe1, 0xdc, 0xff), // sour cream
            new Color32(0xee, 0xe0, 0xfa, 0xff), // cottage cheese
            new Color32(0xff, 0xf1, 0xc2, 0xff)  // cheese
        };

        readonly Sprite[] items = new Sprite[CheeseRules.CheeseLevel + 1];
        readonly List<Object> owned = new List<Object>();

        /// <summary>Rounded box with a dark outline, 9-sliced; tint it with Image.color.</summary>
        public Sprite Box { get; }
        /// <summary>Rounded box without outline.</summary>
        public Sprite Soft { get; }
        public Sprite Mouse { get; }

        public CheeseArt(string gameId)
        {
            for (int level = 1; level <= CheeseRules.CheeseLevel; level++)
                items[level] = LoadSprite($"{gameId}/item_{level}");
            Mouse = LoadSprite($"{gameId}/mouse_hole");
            Box = MakeRounded(96, 30, 7, true);
            Soft = MakeRounded(96, 30, 0, false);
        }

        public Sprite Item(int level) => level > 0 && level < items.Length ? items[level] : null;

        public static Color TileColor(int level) => TileColors[Mathf.Clamp(level, 0, TileColors.Length - 1)];

        public void Dispose()
        {
            foreach (var o in owned)
                if (o != null) Object.Destroy(o);
            owned.Clear();
        }

        Sprite LoadSprite(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[gc26_2048] missing art: Resources/{path}");
                return null;
            }
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            owned.Add(sprite);
            return sprite;
        }

        Sprite MakeRounded(int size, int radius, int outline, bool withOutline)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "gc26_2048_rounded"
            };
            var px = new Color32[size * size];
            Color ink = Ink;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float dx = Mathf.Max(radius - fx, 0f, fx - (size - radius));
                float dy = Mathf.Max(radius - fy, 0f, fy - (size - radius));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 inside
                float alpha = Mathf.Clamp01(0.5f - d);
                Color c = Color.white;
                if (withOutline)
                    c = Color.Lerp(ink, Color.white, Mathf.Clamp01(-d - outline + 0.5f));
                c.a = alpha;
                px[y * size + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            owned.Add(tex);
            float b = radius + 2;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            owned.Add(sprite);
            return sprite;
        }
    }
}
