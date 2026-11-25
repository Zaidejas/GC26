using System.Collections.Generic;
using UnityEngine;

namespace GC26.ScienceSnake
{
    /// <summary>Sprites and sounds generated in code, so the game needs no imported art or audio.</summary>
    public static class Procedural
    {
        // ---------- pixel canvas with simple anti-aliased shapes (y axis points up) ----------
        sealed class Pix
        {
            public readonly int W, H;
            public readonly Color[] C;
            public Pix(int w, int h) { W = w; H = h; C = new Color[w * h]; }

            void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
                int i = y * W + x;
                var d = C[i];
                float sa = c.a * Mathf.Clamp01(a);
                float oa = sa + d.a * (1f - sa);
                if (oa <= 0f) return;
                C[i] = new Color(
                    (c.r * sa + d.r * d.a * (1f - sa)) / oa,
                    (c.g * sa + d.g * d.a * (1f - sa)) / oa,
                    (c.b * sa + d.b * d.a * (1f - sa)) / oa, oa);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color c)
            {
                int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
                int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
                float r = Mathf.Min(rx, ry);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                    float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * r;   // ~pixel distance to the edge
                    Blend(x, y, c, .5f - d);
                }
            }
            public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

            public void Ring(float cx, float cy, float r, float w, Color c)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Abs(Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)) - (r - w / 2f)) - w / 2f;
                    Blend(x, y, c, .5f - d);
                }
            }

            public void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = Mathf.FloorToInt(y0); y < Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
                    Blend(x, y, c, 1f);
            }

            public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
            {
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Blend(x, y, col, 1f);
                }
            }
            static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

            /// <summary>Thick round-capped stroke along a polyline; colour and width blend from start to end.</summary>
            public void Stroke(IList<Vector2> pts, float r0, float r1, Color c0, Color c1)
            {
                float total = 0; for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
                float run = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    float seg = Vector2.Distance(pts[i - 1], pts[i]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(seg / .7f));
                    for (int s = 0; s <= steps; s++)
                    {
                        float k = (run + seg * s / steps) / Mathf.Max(1, total);
                        var p = Vector2.Lerp(pts[i - 1], pts[i], (float)s / steps);
                        Circle(p.x, p.y, Mathf.Lerp(r0, r1, k), Color.Lerp(c0, c1, k));
                    }
                    run += seg;
                }
            }

            /// <summary>Rotated ellipse (angle in radians, counter-clockwise).</summary>
            public void EllipseRot(float cx, float cy, float rx, float ry, float ang, Color c)
            {
                float R = Mathf.Max(rx, ry) + 1, cs = Mathf.Cos(-ang), sn = Mathf.Sin(-ang), r = Mathf.Min(rx, ry);
                for (int y = Mathf.FloorToInt(cy - R); y <= Mathf.CeilToInt(cy + R); y++)
                for (int x = Mathf.FloorToInt(cx - R); x <= Mathf.CeilToInt(cx + R); x++)
                {
                    float px = x + .5f - cx, py = y + .5f - cy;
                    float lx = px * cs - py * sn, ly = px * sn + py * cs;
                    float d = (Mathf.Sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry)) - 1f) * r;
                    Blend(x, y, c, .5f - d);
                }
            }

            /// <summary>Arc of a ring, clockwise from 12 o'clock, covering fraction k of the circle.</summary>
            public void Arc(float cx, float cy, float r, float w, float k, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - r - w); y <= Mathf.CeilToInt(cy + r + w); y++)
                for (int x = Mathf.FloorToInt(cx - r - w); x <= Mathf.CeilToInt(cx + r + w); x++)
                {
                    float px = x + .5f - cx, py = y + .5f - cy;
                    float a = Mathf.Atan2(px, py); if (a < 0) a += Mathf.PI * 2;     // 0 at the top, clockwise
                    float ang = Mathf.PI * 2 * k;
                    float dr = Mathf.Abs(Mathf.Sqrt(px * px + py * py) - r) - w / 2;          // distance outside the ring band
                    float da = k >= 1 || a <= ang ? 0 : Mathf.Min(a - ang, Mathf.PI * 2 - a) * r;   // distance past either arc end
                    Blend(x, y, c, .5f - Mathf.Max(dr, da));
                }
            }

            /// <summary>Fills any shape given as a signed distance (negative inside), with a per-pixel colour.</summary>
            public void Fill(System.Func<float, float, float> sd, System.Func<float, float, Color> col)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = sd(x + .5f, y + .5f);
                    if (d < .5f) Blend(x, y, col(x + .5f, y + .5f), .5f - d);
                }
            }

            public Texture2D Tex(FilterMode f = FilterMode.Trilinear, bool mips = true)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, mips) { filterMode = f, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0 };   // flat 2D sprites never need anisotropic filtering
                t.SetPixels(C, 0);
                if (mips)
                {
                    // Mipmaps are built here on the CPU with premultiplied averaging,
                    // so shapes keep clean edges when drawn small (no dark fringes).
                    var src = C; int w = W, h = H;
                    for (int level = 1; level < t.mipmapCount; level++)
                    {
                        int nw = Mathf.Max(1, w / 2), nh = Mathf.Max(1, h / 2);
                        var dst = new Color[nw * nh];
                        for (int y = 0; y < nh; y++)
                        for (int x = 0; x < nw; x++)
                        {
                            float r = 0, g = 0, b = 0, a = 0; int n = 0;
                            for (int dy = 0; dy < 2; dy++)
                            for (int dx = 0; dx < 2; dx++)
                            {
                                int sx = Mathf.Min(w - 1, x * 2 + dx), sy = Mathf.Min(h - 1, y * 2 + dy);
                                var c = src[sy * w + sx]; r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a; n++;
                            }
                            dst[y * nw + x] = a > 0 ? new Color(r / a, g / a, b / a, a / n) : Color.clear;
                        }
                        t.SetPixels(dst, level);
                        src = dst; w = nw; h = nh;
                    }
                }
                t.Apply(false);
                return t;
            }
        }

        static Sprite Make(Pix p, float ppu) => Sprite.Create(p.Tex(), new Rect(0, 0, p.W, p.H), new Vector2(.5f, .5f), ppu);
        static Sprite MakeSliced(Pix p, float border) =>
            Sprite.Create(p.Tex(FilterMode.Bilinear, false), new Rect(0, 0, p.W, p.H), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));

        /// <summary>Rounded-rectangle coverage (for UI shapes and masks).</summary>
        static Pix RoundedRect(int size, float radius, float blur = 0)
        {
            var p = new Pix(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius + blur, size - radius - blur), cy = Mathf.Clamp(y + .5f, radius + blur, size - radius - blur);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)) - radius;
                float a = blur > 0 ? Mathf.Clamp01(.5f - d / blur) : Mathf.Clamp01(.5f - d);
                if (blur > 0) a = a * a * (3 - 2 * a);                           // smooth falloff for soft shadows
                p.C[y * size + x] = new Color(1, 1, 1, a);
            }
            return p;
        }
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (!cache.TryGetValue(key, out var s) || s == null) cache[key] = s = make();
            return s;
        }

        /// <summary>White circle, 1 world unit in diameter.</summary>
        public static Sprite Circle => Cached("circle", () => { var p = new Pix(128, 128); p.Circle(64, 64, 63, Color.white); return Make(p, 128); });
        /// <summary>Circle for the snake body: crisp edges at small sizes (negative mip bias), so overlapping discs blend into one smooth tube.</summary>
        public static Sprite BodyDisc => Cached("bodydisc", () =>
        {
            var p = new Pix(128, 128); p.Circle(64, 64, 63, Color.white);
            var t = p.Tex(); t.mipMapBias = -1.5f;
            return Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128);
        });
        /// <summary>White square, 1 world unit.</summary>
        public static Sprite Square => Cached("square", () => { var p = new Pix(4, 4); p.Rect(0, 0, 4, 4, Color.white); var t = p.Tex(FilterMode.Bilinear, false); return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4); });

        /// <summary>Timer ring around an item, like the web version: an arc that empties clockwise. 1 unit = 1 cell.</summary>
        public static Sprite Arc(float k)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(k * 48), 0, 48);
            return Cached("arc" + step, () => { var p = new Pix(128, 128); if (step > 0) p.Arc(64, 64, 61.4f - 7.5f, 14f, step / 48f, Color.white); return Make(p, 128); });
        }

        /// <summary>
        /// Snake head seen from above, pointing right: wide at the back, narrower snout (viper shape), dark outline
        /// and a diamond marking on top. Drawn in greys so the SpriteRenderer colour tints it (white = full colour).
        /// 1.25 × 1 units; the pivot sits a little behind the middle, on the cell centre.
        /// </summary>
        public static Sprite SnakeHead => Cached("snakehead", () =>
        {
            var p = new Pix(160, 128);
            Color edge = new Color(.5f, .5f, .5f), fill = Color.white, mark = new Color(.78f, .78f, .78f), light = new Color(1, 1, 1, .35f);
            p.Ellipse(64, 64, 54, 60, edge); p.Ellipse(106, 64, 48, 44, edge);          // outline
            p.Ellipse(64, 64, 49, 55, fill); p.Ellipse(106, 64, 43, 39, fill);          // head
            p.Tri(new Vector2(38, 64), new Vector2(74, 92), new Vector2(110, 64), mark); // diamond marking on top
            p.Tri(new Vector2(38, 64), new Vector2(74, 36), new Vector2(110, 64), mark);
            p.Ellipse(96, 74, 20, 8, light);                                             // soft shine
            return Sprite.Create(p.Tex(), new Rect(0, 0, 160, 128), new Vector2(.42f, .5f), 128);
        });

        /// <summary>Closed "happy" eye: an upward arc (drawn in screen space).</summary>
        public static Sprite EyeSmile => Cached("eyesmile", () =>
        {
            var p = new Pix(64, 64);
            for (int i = 0; i <= 24; i++) { float a = Mathf.Lerp(Mathf.PI * .1f, Mathf.PI * .9f, i / 24f); p.Circle(32 + 22 * Mathf.Cos(a), 22 + 22 * Mathf.Sin(a), 5, Color.white); }
            return Make(p, 64);
        });

        /// <summary>Rounded mask for the board corners: 20 cells wide, corner radius like the web version.</summary>
        public static Sprite BoardMask(int cells) => Cached("boardmask" + cells, () =>
        {
            var p = RoundedRect(512, 512f / cells * .7f);
            return Sprite.Create(p.Tex(FilterMode.Bilinear, false), new Rect(0, 0, 512, 512), new Vector2(.5f, .5f), 512f / cells);
        });

        /// <summary>Soft shadow (blurred rounded rectangle). As a world sprite: 1 unit; as UI: 9-sliced.</summary>
        public static Sprite SoftShadow => Cached("softshadow", () => MakeSliced(RoundedRect(96, 22, 18), 44));

        // apple: two red lobes, highlight, stem and leaf — same proportions as the web version (1 unit = 1 cell)
        public static Sprite Apple => Cached("apple", () =>
        {
            var p = new Pix(128, 128);
            float s = 128, r = .36f * s, cx = 64, cy = 64 - .05f * s;
            Color red = Hex("#d32f2f"), dark = Hex("#b71c1c");
            p.Circle(cx - r * .28f, cy - 1.5f, r * .82f, dark); p.Circle(cx + r * .28f, cy - 1.5f, r * .82f, dark);   // soft lower edge
            p.Circle(cx - r * .28f, cy, r * .82f, red); p.Circle(cx + r * .28f, cy, r * .82f, red);
            p.Circle(cx - r * .42f, cy + r * .3f, r * .2f, new Color(1, 1, 1, .45f));
            var stem = new List<Vector2>(); for (int i = 0; i <= 12; i++) { float t = i / 12f; var a = new Vector2(cx, cy + r * .7f); var c = new Vector2(cx + s * .03f, cy + r * 1.2f); var b = new Vector2(cx + s * .1f, cy + r * 1.35f); stem.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b); }
            p.Stroke(stem, s * .035f, s * .035f, Hex("#5d4037"), Hex("#5d4037"));
            p.EllipseRot(cx + s * .14f, cy + r * 1.05f, s * .12f, s * .06f, .5f, Hex("#43a047"));
            return Make(p, 128);
        });

        public static Sprite Kitten => Cached("kitten", () =>
        {
            var p = new Pix(128, 128);
            Color grey = Hex("#9e9e9e"), dark = Hex("#757575"), pink = Hex("#f48fb1");
            p.Tri(new Vector2(22, 70), new Vector2(32, 118), new Vector2(60, 86), dark);
            p.Tri(new Vector2(106, 70), new Vector2(96, 118), new Vector2(68, 86), dark);
            p.Tri(new Vector2(30, 78), new Vector2(36, 106), new Vector2(54, 88), pink);
            p.Tri(new Vector2(98, 78), new Vector2(92, 106), new Vector2(74, 88), pink);
            p.Ellipse(64, 60, 46, 42, grey);
            p.Ellipse(64, 46, 22, 16, Hex("#eeeeee"));
            foreach (var x in new[] { 46f, 82f }) { p.Ellipse(x, 66, 9, 11, Hex("#c5e1a5")); p.Ellipse(x, 66, 3.5f, 9, Hex("#212121")); p.Circle(x + 3, 71, 2.5f, Color.white); }
            p.Tri(new Vector2(58, 52), new Vector2(70, 52), new Vector2(64, 45), pink);
            foreach (var sgn in new[] { -1f, 1f }) for (int i = -1; i <= 1; i++)
                p.Stroke(new List<Vector2> { new Vector2(64 + sgn * 14, 47 + i * 4), new Vector2(64 + sgn * 40, 50 + i * 8) }, 1.2f, 1.2f, new Color(.25f, .25f, .25f, .8f), new Color(.25f, .25f, .25f, .8f));
            return Make(p, 128);
        });

        public static Sprite Farmer => Cached("farmer", () =>
        {
            var p = new Pix(128, 128);
            Color skin = Hex("#f1c8a0"), hat = Hex("#c8a165"), band = Hex("#8d4b3b"), shirt = Hex("#1e88e5");
            p.Ellipse(64, 14, 40, 18, shirt);                                     // overalls / shoulders
            p.Rect(50, 8, 56, 30, Hex("#1565c0")); p.Rect(72, 8, 78, 30, Hex("#1565c0"));
            p.Ellipse(64, 54, 32, 34, skin);
            p.Ellipse(64, 36, 18, 8, Hex("#e0b48a"));                             // chin shade
            foreach (var x in new[] { 52f, 76f }) p.Circle(x, 58, 4, Hex("#3e2723"));
            var smile = new List<Vector2>(); for (int i = 0; i <= 10; i++) { float a = Mathf.Lerp(Mathf.PI * 1.2f, Mathf.PI * 1.8f, i / 10f); smile.Add(new Vector2(64 + 12 * Mathf.Cos(a), 48 + 6 * Mathf.Sin(a))); }
            p.Stroke(smile, 1.8f, 1.8f, Hex("#5d4037"), Hex("#5d4037"));
            p.Ellipse(64, 80, 60, 12, hat);                                       // straw hat brim
            p.Ellipse(64, 96, 32, 22, hat);
            p.Rect(33, 80, 95, 88, band);
            return Make(p, 128);
        });

        public static Sprite Book => Cached("book", () =>
        {
            var p = new Pix(64, 64);
            Color c = Color.white;
            p.Tri(new Vector2(8, 14), new Vector2(31, 18), new Vector2(31, 52), c); p.Tri(new Vector2(8, 14), new Vector2(8, 48), new Vector2(31, 52), c);
            p.Tri(new Vector2(56, 14), new Vector2(33, 18), new Vector2(33, 52), c); p.Tri(new Vector2(56, 14), new Vector2(56, 48), new Vector2(33, 52), c);
            return Make(p, 64);
        });
        /// <summary>Arrow for the on-screen keys (points up; rotate for other directions). Same shape for every key.</summary>
        public static Sprite Arrow => Cached("arrow", () => { var p = new Pix(64, 64); p.Tri(new Vector2(12, 18), new Vector2(52, 18), new Vector2(32, 48), Color.white); return Make(p, 64); });
        public static Sprite Play => Cached("play", () => { var p = new Pix(64, 64); p.Tri(new Vector2(20, 12), new Vector2(20, 52), new Vector2(54, 32), Color.white); return Make(p, 64); });
        public static Sprite PauseIcon => Cached("pause", () => { var p = new Pix(64, 64); p.Rect(17, 12, 27, 52, Color.white); p.Rect(37, 12, 47, 52, Color.white); return Make(p, 64); });

        /// <summary>Vertical gradient for the menu background (top → bottom).</summary>
        public static Sprite Gradient(Color top, Color bottom) => Cached("grad" + top + bottom, () =>
        {
            var p = new Pix(4, 64);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) p.C[y * 4 + x] = Color.Lerp(bottom, top, y / 63f);
            return Sprite.Create(p.Tex(FilterMode.Bilinear, false), new Rect(0, 0, 4, 64), new Vector2(.5f, .5f), 100);
        });

        /// <summary>Rounded rectangle for UI panels and buttons (9-sliced).</summary>
        public static Sprite RoundRect => Cached("roundrect", () =>
        {
            var p = new Pix(48, 48);
            float r = 16;
            for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                float cx = Mathf.Clamp(x + .5f, r, 48 - r), cy = Mathf.Clamp(y + .5f, r, 48 - r);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)) - (r - .5f);
                p.C[y * 48 + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d));
            }
            return Sprite.Create(p.Tex(), new Rect(0, 0, 48, 48), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
        });

        /// <summary>
        /// Menu logo, app-icon style: a deep green rounded square with one smooth cream "S"-shaped snake
        /// that looks straight at the player, and a small red apple. Few shapes, strong contrast, reads well when small.
        /// </summary>
        public static Sprite Logo => Cached("logo", () =>
        {
            const int S = 400;                                     // drawn large and shown at ~130 px, mipmaps keep it smooth
            var p = new Pix(S, S);
            Color top = Hex("#4caf50"), bottom = Hex("#1b5e20"), cream = Hex("#f1f8e9"), shade = Hex("#c5e1a5"), ink = Hex("#1b3d1e");

            // badge: rounded square with a vertical gradient and a soft shine on the upper half
            float Badge(float x, float y) { float cx = Mathf.Clamp(x, 92, 308), cy = Mathf.Clamp(y, 92, 308); return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - 84; }
            p.Fill(Badge, (x, y) => Color.Lerp(bottom, top, Mathf.Clamp01((y - 20) / 360f)));
            p.Fill((x, y) => Mathf.Max(Badge(x, y) + 10, EllipseSd(x - 200, y - 330, 230, 120)), (x, y) => new Color(1, 1, 1, .07f));

            // body: one "S" from under the chin down to a tapering tail
            var path = new List<Vector2>();
            void Bez(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 a3)
            {
                for (int i = path.Count == 0 ? 0 : 1; i <= 48; i++)
                {
                    float t = i / 48f, u = 1 - t;
                    path.Add(u * u * u * a0 + 3 * u * u * t * a1 + 3 * u * t * t * a2 + t * t * t * a3);
                }
            }
            Bez(new Vector2(200, 250), new Vector2(105, 248), new Vector2(98, 168), new Vector2(200, 166));
            Bez(new Vector2(200, 166), new Vector2(302, 164), new Vector2(300, 84), new Vector2(200, 84));
            Bez(new Vector2(200, 84), new Vector2(160, 84), new Vector2(130, 88), new Vector2(104, 100));
            float total = 0; var acc = new float[path.Count];
            for (int i = 1; i < path.Count; i++) { total += Vector2.Distance(path[i - 1], path[i]); acc[i] = total; }
            float Wid(int i) { float k = acc[i] / total; return k < .72f ? 31f : Mathf.Lerp(31f, 7f, Mathf.SmoothStep(0, 1, (k - .72f) / .28f)); }

            float Body(float x, float y) { float m = 1e9f; for (int i = 0; i < path.Count; i += 2) m = Mathf.Min(m, Vector2.Distance(new Vector2(x, y), path[i]) - Wid(i)); return m; }
            p.Fill((x, y) => Body(x - 3, y + 9), (x, y) => new Color(0, 0, 0, .16f));                       // soft drop shadow
            for (int i = 0; i < path.Count; i++) p.Circle(path[i].x, path[i].y, Wid(i), shade);
            for (int i = 0; i < path.Count; i++) { float w = Wid(i); p.Circle(path[i].x - w * .06f, path[i].y + w * .14f, w * .84f, cream); }
            // a calm row of scales along the back
            for (float d = 40; d < total * .8f; d += 30)
            {
                int i = 1; while (i < path.Count - 1 && acc[i] < d) i++;
                p.Circle(path[i].x, path[i].y + 2, 6.5f, new Color(.55f, .76f, .45f, .75f));
            }

            // head, facing the player
            float Head(float x, float y) => SMin(EllipseSd(x - 200, y - 292, 74, 56), EllipseSd(x - 200, y - 256, 50, 40), 20);
            p.Fill((x, y) => Head(x + 3, y + 9), (x, y) => new Color(0, 0, 0, .16f));
            p.Fill(Head, (x, y) => Color.Lerp(shade, cream, Mathf.Clamp01((y - 215) / 50f)));
            foreach (var ex in new[] { 170f, 230f })
            {
                p.Ellipse(ex, 296, 12, 16, ink);
                p.Circle(ex + 4, 302, 4.2f, Color.white);
            }
            p.Circle(190, 252, 3, new Color(.11f, .24f, .12f, .55f)); p.Circle(210, 252, 3, new Color(.11f, .24f, .12f, .55f));   // nostrils
            Color tr = Hex("#e53935");
            p.Stroke(new List<Vector2> { new Vector2(200, 222), new Vector2(200, 198) }, 3.6f, 3.4f, tr, tr);
            p.Stroke(new List<Vector2> { new Vector2(200, 200), new Vector2(191, 186) }, 3.4f, 2.2f, tr, tr);
            p.Stroke(new List<Vector2> { new Vector2(200, 200), new Vector2(209, 186) }, 3.4f, 2.2f, tr, tr);

            // apple in the upper right corner
            Color ar = Hex("#e53935"), ad = Hex("#b71c1c");
            p.Circle(308, 318, 23, ad); p.Circle(318, 318, 23, ad); p.Circle(306, 320, 21, ar); p.Circle(320, 320, 21, ar);
            p.Ellipse(301, 328, 5, 7, new Color(1, 1, 1, .5f));
            p.Stroke(new List<Vector2> { new Vector2(313, 338), new Vector2(316, 352) }, 2.6f, 2.6f, Hex("#3e2723"), Hex("#3e2723"));
            p.EllipseRot(328, 350, 11, 5, .45f, Hex("#a5d6a7"));
            return Make(p, 200);
        });
        static float EllipseSd(float px, float py, float rx, float ry)
        {
            float k0 = Mathf.Sqrt(px * px / (rx * rx) + py * py / (ry * ry));
            float k1 = Mathf.Sqrt(px * px / (rx * rx * rx * rx) + py * py / (ry * ry * ry * ry));
            return k1 < 1e-5f ? -Mathf.Min(rx, ry) : k0 * (k0 - 1f) / k1;
        }
        static float SMin(float a, float b, float k) { float h = Mathf.Clamp01(.5f + .5f * (b - a) / k); return Mathf.Lerp(b, a, h) - k * h * (1 - h); }

        /// <summary>The 20×20 checkered field. Black cells are eaten by the evil snake.</summary>
        public static Texture2D FieldTexture(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            return t;
        }

        // ---------- sounds ----------
        public struct Tone { public float f0, f1, dur, vol, delay; public int wave; }   // wave: 0 sine, 1 triangle, 2 saw, 3 square

        public static AudioClip Synth(string name, params Tone[] tones)
        {
            const int rate = 44100;
            float len = 0f;
            foreach (var t in tones) len = Mathf.Max(len, t.delay + t.dur + .03f);
            var data = new float[Mathf.CeilToInt(len * rate)];
            foreach (var t in tones)
            {
                int start = Mathf.FloorToInt(t.delay * rate), n = Mathf.FloorToInt(t.dur * rate);
                double phase = 0;
                for (int i = 0; i < n && start + i < data.Length; i++)
                {
                    float k = (float)i / n;
                    float f = t.f1 > 0 ? t.f0 * Mathf.Pow(t.f1 / t.f0, k) : t.f0;
                    phase += f / rate;
                    float ph = (float)(phase - System.Math.Floor(phase));
                    float w = t.wave switch
                    {
                        1 => 1f - 4f * Mathf.Abs(ph - .5f),
                        2 => 2f * ph - 1f,
                        3 => ph < .5f ? 1f : -1f,
                        _ => Mathf.Sin(ph * Mathf.PI * 2f),
                    };
                    float env = t.vol * Mathf.Pow(.0001f / Mathf.Max(t.vol, .0001f), k);   // exponential decay, like the web version
                    data[start + i] += w * env;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
