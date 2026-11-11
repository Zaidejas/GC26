using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Placeholder pixel art generated in code (same style as the HTML prototype), so the first
    /// playable build needs no imported assets. 16 art pixels = 1 world unit = 1 room cell.
    /// Owned by BomberContext; textures are destroyed with it (no static caches).
    /// </summary>
    public sealed class BomberArt : IDisposable
    {
        public const int PixelsPerUnit = 16;

        public static readonly Color32 Void = Hex("#060910");
        public static readonly Color32 PlayerColor = Hex("#7fe3ff");
        public static readonly Color32 MetalDark = Hex("#2b3348");
        public static readonly Color32 Metal = Hex("#4d5a78");
        public static readonly Color32 MetalLight = Hex("#8a9bc0");
        public static readonly Color32 Black = Hex("#070912");
        public static readonly Color32 Outline = Hex("#05070e");

        public Sprite Drone { get; private set; }
        public Sprite Rotor { get; private set; }
        public Sprite Gun { get; private set; }
        public Sprite Bullet { get; private set; }
        public Sprite BulletHeavy { get; private set; }
        public Sprite Dummy { get; private set; }
        public Sprite Wall { get; private set; }
        public Sprite Pixel { get; private set; }

        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        public BomberArt()
        {
            Drone = MakeDrone(PlayerColor);
            Rotor = Make(9, 1, c => { for (int x = 0; x < 9; x++) if (x != 4) c.Set(x, 0, Hex("#e6eeff")); }, false);
            Gun = Make(9, 4, c => { c.FillRect(0, 0, 8, 3, MetalDark); c.FillRect(1, 1, 6, 2, Metal); c.FillRect(6, 1, 7, 2, PlayerColor); }, true, new Vector2(0.1f, 0.5f));
            Bullet = Make(4, 4, c => { c.FillRect(0, 0, 3, 3, Hex("#7fe3ff")); c.FillRect(1, 1, 2, 2, Hex("#ffffff")); }, false);
            BulletHeavy = Make(6, 6, c => { c.FillCircle(3, 3, 3, Hex("#7fe3ff")); c.FillCircle(3, 3, 1.6f, Hex("#ffffff")); }, false);
            Dummy = MakeDummy();
            Wall = MakeWall();
            Pixel = Make(1, 1, c => c.Set(0, 0, new Color32(255, 255, 255, 255)), false);
        }

        public void Dispose()
        {
            foreach (var o in _owned) if (o) UnityEngine.Object.Destroy(o);
            _owned.Clear();
        }

        // ---------- sprite builders ----------
        Sprite MakeDrone(Color32 body)
        {
            return Make(20, 20, c =>
            {
                const float cx = 10f, cy = 10f, hub = 5.5f;
                foreach (var s in Corners) c.Line(cx, cy, cx + s.x * hub, cy + s.y * hub, MetalDark, 2);
                foreach (var s in Corners) { c.FillCircle(cx + s.x * hub, cy + s.y * hub, 2.2f, Metal); c.Set((int)(cx + s.x * hub), (int)(cy + s.y * hub), Black); }
                c.FillCircleShaded(cx, cy, 4.6f, body);
                c.Set(11, 12, Hex("#ffffff")); c.Set(10, 11, Hex("#ffffff")); c.Set(11, 10, Hex("#ffffff")); c.Set(10, 9, Hex("#ffffff"));
                c.FillCircle(cx, cy, 1.9f, Black);
            });
        }

        Sprite MakeDummy()
        {
            var col = Hex("#ffcf5c");
            return Make(16, 16, c =>
            {
                c.FillRect(2, 2, 14, 14, Metal); c.FillRect(4, 4, 12, 12, MetalDark);
                foreach (var p in new[] { new Vector2Int(2, 2), new Vector2Int(13, 2), new Vector2Int(2, 13), new Vector2Int(13, 13) }) c.Set(p.x, p.y, MetalLight);
                c.FillCircleShaded(8, 8, 4f, col);
                c.Set(9, 8, Hex("#ff3048")); c.Set(9, 9, Hex("#ff3048"));
            });
        }

        Sprite MakeWall()
        {
            return Make(16, 16, c =>
            {
                c.FillRect(0, 0, 16, 16, Hex("#1b2742"));
                c.FillRect(0, 14, 16, 16, Hex("#2c3d63"));
                c.FillRect(0, 0, 16, 1, Hex("#121a2e")); c.FillRect(15, 0, 16, 16, Hex("#121a2e"));
                c.FillRect(0, 1, 1, 14, Hex("#24335a"));
                foreach (var p in new[] { new Vector2Int(3, 11), new Vector2Int(12, 11), new Vector2Int(3, 3), new Vector2Int(12, 3) }) c.Set(p.x, p.y, Hex("#3a4c78"));
                c.FillRect(5, 7, 11, 8, Hex("#16203a"));
            }, false, null, SpriteMeshType.FullRect);
        }

        /// <summary>Floor of a room of the given size in cells, with per-tile variation (seeded).</summary>
        public Sprite MakeFloor(int cellsW, int cellsH, Color32 floor, Color32 accent, int seed)
        {
            var rnd = new System.Random(seed);
            return Make(cellsW * 16, cellsH * 16, c =>
            {
                for (int ty = 0; ty < cellsH; ty++)
                for (int tx = 0; tx < cellsW; tx++)
                {
                    int ox = tx * 16, oy = ty * 16; float v = (float)rnd.NextDouble() * 8f - 4f;
                    c.FillRect(ox, oy, ox + 16, oy + 16, Shade(floor, v));
                    c.FillRect(ox, oy + 15, ox + 16, oy + 16, Shade(floor, v + 9)); c.FillRect(ox, oy, ox + 1, oy + 16, Shade(floor, v + 9));
                    c.FillRect(ox, oy, ox + 16, oy + 1, Shade(floor, v - 12)); c.FillRect(ox + 15, oy, ox + 16, oy + 16, Shade(floor, v - 12));
                    double r = rnd.NextDouble();
                    if (r < 0.08) { for (int k = 3; k < 13; k += 2) c.FillRect(ox + 3, oy + k, ox + 13, oy + k + 1, Shade(floor, v - 16)); }
                    else if (r < 0.16) { var cc = Shade(floor, v + 18); c.Set(ox + 2, oy + 2, cc); c.Set(ox + 13, oy + 2, cc); c.Set(ox + 2, oy + 13, cc); c.Set(ox + 13, oy + 13, cc); }
                    for (int k = 0; k < 3; k++) c.Set(ox + 1 + rnd.Next(14), oy + 1 + rnd.Next(14), Shade(floor, rnd.NextDouble() < 0.5 ? -8 : 8));
                }
                var a = accent; a.a = 64;
                for (int i = 0; i < 3; i++)
                {
                    double y0 = 30 + rnd.NextDouble() * (cellsH * 16 - 60), amp = 10 + rnd.NextDouble() * 30, ph = rnd.NextDouble() * 6;
                    for (int x = 0; x < cellsW * 16; x++) { int y = (int)(y0 + Math.Sin(x / 70.0 + ph) * amp); c.Blend(x, y, a); c.Blend(x, y + 1, a); }
                }
            }, false, null, SpriteMeshType.FullRect);
        }

        static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };

        Sprite Make(int w, int h, Action<PixelCanvas> draw, bool outline = true, Vector2? pivot = null, SpriteMeshType mesh = SpriteMeshType.Tight)
        {
            var c = new PixelCanvas(w + (outline ? 2 : 0), h + (outline ? 2 : 0), outline ? 1 : 0);
            draw(c);
            if (outline) c.AddOutline(Outline);
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "gc26_bomber_px" };
            tex.SetPixels32(c.Pixels); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, c.W, c.H), pivot ?? new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, mesh);
            _owned.Add(tex); _owned.Add(sprite);
            return sprite;
        }

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static Color32 Shade(Color32 c, double v)
        {
            byte Cl(double x) => (byte)Math.Max(0, Math.Min(255, x));
            return new Color32(Cl(c.r + v), Cl(c.g + v), Cl(c.b + v * 1.3), 255);
        }

        /// <summary>Tiny CPU canvas; y goes up like Unity textures.</summary>
        sealed class PixelCanvas
        {
            public readonly int W, H; readonly int _o;
            public readonly Color32[] Pixels;
            public PixelCanvas(int w, int h, int offset) { W = w; H = h; _o = offset; Pixels = new Color32[w * h]; }

            public void Set(int x, int y, Color32 c) { x += _o; y += _o; if (x >= 0 && y >= 0 && x < W && y < H) Pixels[y * W + x] = c; }
            public void Blend(int x, int y, Color32 c)
            {
                x += _o; y += _o; if (x < 0 || y < 0 || x >= W || y >= H) return;
                var d = Pixels[y * W + x]; float a = c.a / 255f;
                Pixels[y * W + x] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
            }
            public void FillRect(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Set(x, y, c); }
            public void FillCircle(float cx, float cy, float r, Color32 c)
            {
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) < r * r) Set(x, y, c);
            }
            /// <summary>Circle with a light rim top-left and a dark rim bottom-right.</summary>
            public void FillCircleShaded(float cx, float cy, float r, Color32 c)
            {
                var light = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.42f);
                var dark = Color32.Lerp(c, new Color32(6, 8, 20, 255), 0.42f);
                bool In(int x, int y) => (x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) < r * r;
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                {
                    if (!In(x, y)) continue;
                    var k = c;
                    if (!In(x, y + 1) || !In(x - 1, y)) k = light;
                    else if (!In(x, y - 1) || !In(x + 1, y)) k = dark;
                    Set(x, y, k);
                }
            }
            public void Line(float x0, float y0, float x1, float y1, Color32 c, int t = 1)
            {
                int n = (int)(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 2) + 1;
                for (int i = 0; i <= n; i++) { float k = i / (float)n; int x = (int)(x0 + (x1 - x0) * k - (t - 1) * 0.5f), y = (int)(y0 + (y1 - y0) * k - (t - 1) * 0.5f); FillRect(x, y, x + t, y + t, c); }
            }
            public void AddOutline(Color32 c)
            {
                var add = new List<int>();
                bool A(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Pixels[y * W + x].a > 0;
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                    if (!A(x, y) && (A(x - 1, y) || A(x + 1, y) || A(x, y - 1) || A(x, y + 1))) add.Add(y * W + x);
                foreach (var i in add) Pixels[i] = c;
            }
        }
    }
}
