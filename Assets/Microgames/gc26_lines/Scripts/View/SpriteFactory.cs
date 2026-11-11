using System.Collections.Generic;
using GC26.Lines.Core;
using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// Draws the game's sprites in code (no image files): glossy balls and rounded squares.
    /// The glossy balls are the fallback when the mushroom images are missing (see PieceSet).
    /// Sprites are cached, so each one is drawn only once.
    /// </summary>
    public static class SpriteFactory
    {
        const int BallTextureSize = 128;
        const int RoundTextureSize = 64;
        const int RoundRadiusPx = 16;

        static readonly Dictionary<BallColor, Sprite> Balls = new Dictionary<BallColor, Sprite>();
        static readonly Dictionary<float, Sprite> RoundRects = new Dictionary<float, Sprite>();
        static Texture2D roundTexture;
        static Sprite pixel;

        /// <summary>A glossy ball, 1 world unit wide, like the prototype's balls (radial gradient + highlight).</summary>
        public static Sprite Ball(BallColor color)
        {
            if (Balls.TryGetValue(color, out var cached) && cached != null) return cached;
            var (basis, light, dark) = Palette.Ball(color);
            int n = BallTextureSize;
            var pixels = new Color32[n * n];
            // Highlight centre at 34% from the left and 30% from the top, as in the prototype's CSS.
            var highlight = new Vector2(0.34f, 0.70f);
            const float farthest = 0.962f; // distance from the highlight to the farthest corner
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float edge = (0.5f - Vector2.Distance(p, new Vector2(0.5f, 0.5f))) * n; // pixels inside the edge
                float alpha = Mathf.Clamp01(edge);
                var c = Gradient(Vector2.Distance(p, highlight) / farthest, basis, light, dark);
                c.a = alpha;
                pixels[y * n + x] = c;
            }
            var sprite = Sprite.Create(NewTexture(n, pixels, "Ball " + color), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.name = "Ball " + color;
            Balls[color] = sprite;
            return sprite;
        }

        // Stops: 0–7% white, 18% light, 55% base, 100% dark.
        static Color Gradient(float d, Color basis, Color light, Color dark)
        {
            if (d <= 0.07f) return Color.white;
            if (d <= 0.18f) return Color.Lerp(Color.white, light, (d - 0.07f) / 0.11f);
            if (d <= 0.55f) return Color.Lerp(light, basis, (d - 0.18f) / 0.37f);
            return Color.Lerp(basis, dark, Mathf.Clamp01((d - 0.55f) / 0.45f));
        }

        /// <summary>
        /// A white rounded rectangle for 9-slice drawing (SpriteRenderer.drawMode = Sliced),
        /// with corners <paramref name="cornerRadius"/> world units round at any size.
        /// </summary>
        public static Sprite RoundRect(float cornerRadius)
        {
            if (RoundRects.TryGetValue(cornerRadius, out var cached) && cached != null) return cached;
            if (roundTexture == null) roundTexture = MakeRoundTexture();
            float pixelsPerUnit = RoundRadiusPx / cornerRadius;
            var border = new Vector4(RoundRadiusPx, RoundRadiusPx, RoundRadiusPx, RoundRadiusPx);
            var sprite = Sprite.Create(roundTexture, new Rect(0, 0, RoundTextureSize, RoundTextureSize),
                new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = "RoundRect " + cornerRadius;
            RoundRects[cornerRadius] = sprite;
            return sprite;
        }

        // ---- Soft glows (the prototype's box-shadow glows) ----

        // Rounded enough to follow pill-shaped buttons: the middle has 30 px corners, the fade is 26 px.
        const int GlowFadePx = 26, GlowCornerPx = 30, GlowEdgePx = GlowFadePx + GlowCornerPx, GlowTextureSize = 2 * GlowEdgePx + 16;
        static Texture2D glowTexture;
        static readonly Dictionary<float, Sprite> Glows = new Dictionary<float, Sprite>();
        static Sprite softDisc;

        /// <summary>
        /// A white glow for 9-slice drawing: solid in the middle, fading out towards the edges over
        /// <paramref name="edge"/> world units. Put it behind a shape, larger than the shape, for a halo.
        /// </summary>
        public static Sprite SoftGlow(float edge)
        {
            if (Glows.TryGetValue(edge, out var cached) && cached != null) return cached;
            if (glowTexture == null) glowTexture = MakeGlowTexture();
            int n = GlowTextureSize;
            var sprite = Sprite.Create(glowTexture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), GlowEdgePx / edge,
                0, SpriteMeshType.FullRect, new Vector4(GlowEdgePx, GlowEdgePx, GlowEdgePx, GlowEdgePx));
            sprite.name = "SoftGlow " + edge;
            Glows[edge] = sprite;
            return sprite;
        }

        /// <summary>How much of the 9-slice edge in <see cref="SoftGlow"/> is the fade (the rest is the rounded corner).</summary>
        public const float SoftGlowFadeShare = (float)GlowFadePx / GlowEdgePx;

        static Texture2D MakeGlowTexture()
        {
            int n = GlowTextureSize;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Distance from the solid middle (a rounded square), faded with an ease-out curve.
                // The solid middle starts GlowFadePx in from the edge, so the glow is fully clear at the edge.
                float dx = Mathf.Max(GlowFadePx + GlowCornerPx - (x + 0.5f), (x + 0.5f) - (n - GlowFadePx - GlowCornerPx), 0f);
                float dy = Mathf.Max(GlowFadePx + GlowCornerPx - (y + 0.5f), (y + 0.5f) - (n - GlowFadePx - GlowCornerPx), 0f);
                float d = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dy * dy) - GlowCornerPx);
                float t = 1f - Mathf.Clamp01(d / GlowFadePx);
                pixels[y * n + x] = new Color(1f, 1f, 1f, t * t);
            }
            return NewTexture(n, pixels, "SoftGlow");
        }

        /// <summary>A white disc that fades out from the middle, 1 world unit wide (the light inside each cell).</summary>
        public static Sprite SoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int n = 64;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float t = 1f - Mathf.Clamp01(d);
                pixels[y * n + x] = new Color(1f, 1f, 1f, t * t);
            }
            softDisc = Sprite.Create(NewTexture(n, pixels, "SoftDisc"), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return softDisc;
        }

        static Sprite ring;

        /// <summary>A white rounded outline for 9-slice drawing: 16 px corners, a 3 px line, empty middle.</summary>
        public static Sprite RoundRectRing()
        {
            if (ring != null) return ring;
            int n = RoundTextureSize;
            float r = RoundRadiusPx, thickness = 3f;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float outer = RoundedInside(x, y, n, r, 0f), inner = RoundedInside(x, y, n, r - thickness, thickness);
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(outer - inner));
            }
            ring = Sprite.Create(NewTexture(n, pixels, "RoundRectRing"), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(RoundRadiusPx, RoundRadiusPx, RoundRadiusPx, RoundRadiusPx));
            return ring;
        }

        // How much of pixel (x, y) is inside a rounded square inset by `inset` px with corner radius r (0–1).
        static float RoundedInside(int x, int y, int n, float r, float inset)
        {
            float lo = inset + r, hi = n - inset - r;
            float px = Mathf.Max(lo - (x + 0.5f), (x + 0.5f) - hi, 0f);
            float py = Mathf.Max(lo - (y + 0.5f), (y + 0.5f) - hi, 0f);
            bool inBox = x + 0.5f > inset && x + 0.5f < n - inset && y + 0.5f > inset && y + 0.5f < n - inset;
            if (!inBox) return 0f;
            return Mathf.Clamp01(r - Mathf.Sqrt(px * px + py * py) + 0.5f);
        }

        static Sprite disc;

        /// <summary>A plain white circle, 1 world unit wide (burst particles).</summary>
        public static Sprite Disc()
        {
            if (disc != null) return disc;
            const int n = 32;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - d));
            }
            disc = Sprite.Create(NewTexture(n, pixels, "Disc"), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return disc;
        }

        static Sprite undoIcon, resetIcon;

        /// <summary>The prototype's Undo icon: a hook arrow pointing left.</summary>
        public static Sprite UndoIcon() => undoIcon != null ? undoIcon : (undoIcon = StrokeIcon("Undo", 2.6f,
            new[] { new Vector2(9, 14), new Vector2(4, 9), new Vector2(9, 4) },
            Join(new[] { new Vector2(4, 9), new Vector2(14.5f, 9) }, Arc(new Vector2(14.5f, 14.5f), 5.5f, -90f, 90f), new[] { new Vector2(11, 20) })));

        /// <summary>The prototype's Reset icon: a circle arrow turning back.</summary>
        public static Sprite ResetIcon() => resetIcon != null ? resetIcon : (resetIcon = StrokeIcon("Reset", 2.6f,
            Join(Arc(new Vector2(12, 12), 9f, 180f, -131.8f), new[] { new Vector2(3, 8) }),
            new[] { new Vector2(3, 3), new Vector2(3, 8), new Vector2(8, 8) }));

        static Sprite homeIcon;

        /// <summary>
        /// The prototype's house icon for the game screen's "main menu" button: a roof and a house with a
        /// small door and rounded bottom corners (SVG paths on a 24-unit grid, stroke 2.4).
        /// </summary>
        public static Sprite HomeIcon() => homeIcon != null ? homeIcon : (homeIcon = StrokeIcon("Home", 2.4f,
            new[] { new Vector2(3.5f, 11f), new Vector2(12f, 3.8f), new Vector2(20.5f, 11f) },
            Join(new[] { new Vector2(5.8f, 9.6f), new Vector2(5.8f, 19f) },
                Arc(new Vector2(7f, 19f), 1.2f, 180f, 90f),
                new[] { new Vector2(10.4f, 20.2f), new Vector2(10.4f, 14.8f), new Vector2(13.6f, 14.8f), new Vector2(13.6f, 20.2f), new Vector2(17f, 20.2f) },
                Arc(new Vector2(17f, 19f), 1.2f, 90f, 0f),
                new[] { new Vector2(18.2f, 9.6f) })));

        // Points along a circle (SVG coordinates: y points down, angles in degrees).
        static Vector2[] Arc(Vector2 centre, float radius, float fromDeg, float toDeg)
        {
            int steps = Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 6f);
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return points;
        }

        static Vector2[] Join(params Vector2[][] parts)
        {
            var all = new List<Vector2>();
            foreach (var part in parts) all.AddRange(part);
            return all.ToArray();
        }

        // A white icon made of round-ended lines through the given points (a 24-unit grid, like the prototype's SVGs).
        static Sprite StrokeIcon(string name, float strokeWidth, params Vector2[][] lines)
        {
            const int n = 64;
            float scale = n / 24f, half = strokeWidth / 2f * scale;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, n - (y + 0.5f)); // SVG y points down
                float d = float.MaxValue;
                foreach (var line in lines)
                    for (int k = 1; k < line.Length; k++)
                        d = Mathf.Min(d, DistanceToSegment(p, line[k - 1] * scale, line[k] * scale));
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(half - d + 0.5f));
            }
            var sprite = Sprite.Create(NewTexture(n, pixels, name), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.name = name;
            return sprite;
        }

        static Sprite chevron;

        /// <summary>A white "‹" arrow with rounded ends, like the prototype's back button icon.</summary>
        public static Sprite BackArrow()
        {
            if (chevron != null) return chevron;
            const int n = 64;
            // The prototype's SVG path "M15 5 l-7 7 7 7" on a 24-unit grid, stroke 2.8.
            var a = new Vector2(15f, 5f) * n / 24f;
            var b = new Vector2(8f, 12f) * n / 24f;
            var c = new Vector2(15f, 19f) * n / 24f;
            float half = 2.8f / 2f * n / 24f;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, n - (y + 0.5f)); // SVG y points down
                float d = Mathf.Min(DistanceToSegment(p, a, b), DistanceToSegment(p, b, c));
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(half - d + 0.5f));
            }
            chevron = Sprite.Create(NewTexture(n, pixels, "BackArrow"), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return chevron;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            if (ab.sqrMagnitude < 1e-6f) return Vector2.Distance(p, a); // a zero-length piece (two equal points)
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A plain white square, 1 world unit wide.</summary>
        public static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            var pixels = new Color32[4 * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            pixel = Sprite.Create(NewTexture(4, pixels, "Pixel"), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return pixel;
        }

        static Texture2D MakeRoundTexture()
        {
            int n = RoundTextureSize;
            float r = RoundRadiusPx;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Distance outside the rounded corner (only near the corners).
                float px = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r), 0f);
                float py = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r), 0f);
                float alpha = Mathf.Clamp01(r - Mathf.Sqrt(px * px + py * py) + 0.5f);
                pixels[y * n + x] = new Color(1f, 1f, 1f, alpha);
            }
            return NewTexture(n, pixels, "RoundRect");
        }

        static Texture2D NewTexture(int size, Color32[] pixels, string name)
        {
            // linear: false → colors are stored as written (sRGB), matching the prototype's hex values.
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
