using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.View
{
    /// <summary>Badges for special pieces, computed at start-up from distance shapes.</summary>
    public static class SpecialIcons
    {
        const int Size = 96;

        static Sprite bomb, lightning, freeze;

        public static readonly Color BombColor = new Color(1f, 0.42f, 0.24f);
        public static readonly Color LightningColor = new Color(1f, 0.9f, 0.32f);
        public static readonly Color FreezeColor = new Color(0.5f, 0.88f, 1f);

        public static Sprite Get(SpecialKind kind)
        {
            switch (kind)
            {
                case SpecialKind.Bomb: return bomb != null ? bomb : (bomb = Build(PaintBomb));
                case SpecialKind.Lightning: return lightning != null ? lightning : (lightning = Build(PaintLightning));
                case SpecialKind.Freeze: return freeze != null ? freeze : (freeze = Build(PaintFreeze));
                default: return null;
            }
        }

        public static Color ColorOf(SpecialKind kind)
        {
            switch (kind)
            {
                case SpecialKind.Bomb: return BombColor;
                case SpecialKind.Lightning: return LightningColor;
                case SpecialKind.Freeze: return FreezeColor;
                default: return Color.white;
            }
        }

        delegate Color Painter(float x, float y);

        static Sprite Build(Painter paint)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f;
                    float y = (j + 0.5f) / Size * 2f - 1f;
                    texture.SetPixel(i, j, paint(x, y));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
        }

        // --- shape helpers ----------------------------------------------------------------

        const float AA = 0.035f;

        static float Fill(float distance)
        {
            return Mathf.Clamp01(0.5f - distance / AA);
        }

        static float Circle(float x, float y, float cx, float cy, float r)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float ex = px - dx * t, ey = py - dy * t;
            return Mathf.Sqrt(ex * ex + ey * ey) - r;
        }

        static float Polygon(float x, float y, Vector2[] points)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i, i++)
            {
                Vector2 a = points[i], b = points[j];
                best = Mathf.Min(best, Segment(x, y, a.x, a.y, b.x, b.y, 0f));
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside ? -best : best;
        }

        static Color Over(Color under, Color top, float coverage)
        {
            float a = top.a * coverage;
            float outA = a + under.a * (1f - a);
            if (outA <= 0.0001f)
                return new Color(top.r, top.g, top.b, 0f);
            return new Color(
                (top.r * a + under.r * under.a * (1f - a)) / outA,
                (top.g * a + under.g * under.a * (1f - a)) / outA,
                (top.b * a + under.b * under.a * (1f - a)) / outA,
                outA);
        }

        // A dark disc with a bright rim keeps the badge readable on every piece colour.
        static Color Badge(float x, float y, Color rim)
        {
            float disc = Circle(x, y, 0f, 0f, 0.86f);
            Color c = new Color(rim.r, rim.g, rim.b, 0f);
            c = Over(c, rim, Fill(disc));
            return Over(c, new Color(0.08f, 0.06f, 0.12f, 1f), Fill(disc + 0.1f));
        }

        // --- badges -----------------------------------------------------------------------

        static Color PaintBomb(float x, float y)
        {
            Color c = Badge(x, y, BombColor);
            c = Over(c, new Color(0.62f, 0.6f, 0.68f), Fill(Circle(x, y, -0.06f, -0.14f, 0.42f)));
            c = Over(c, new Color(0.26f, 0.24f, 0.32f), Fill(Circle(x, y, -0.06f, -0.14f, 0.35f)));
            c = Over(c, new Color(0.72f, 0.7f, 0.78f), Fill(Circle(x, y, -0.18f, -0.02f, 0.08f)));
            c = Over(c, new Color(0.75f, 0.6f, 0.4f), Fill(Segment(x, y, 0.15f, 0.2f, 0.36f, 0.42f, 0.045f)));
            c = Over(c, new Color(1f, 0.55f, 0.15f), Fill(Circle(x, y, 0.4f, 0.46f, 0.15f)));
            return Over(c, new Color(1f, 0.95f, 0.6f), Fill(Circle(x, y, 0.4f, 0.46f, 0.07f)));
        }

        static readonly Vector2[] Bolt =
        {
            new Vector2(0.14f, 0.68f), new Vector2(-0.36f, -0.08f), new Vector2(-0.04f, -0.08f),
            new Vector2(-0.16f, -0.68f), new Vector2(0.38f, 0.14f), new Vector2(0.06f, 0.14f),
        };

        static Color PaintLightning(float x, float y)
        {
            Color c = Badge(x, y, LightningColor);
            return Over(c, LightningColor, Fill(Polygon(x, y, Bolt)));
        }

        static Color PaintFreeze(float x, float y)
        {
            Color c = Badge(x, y, FreezeColor);
            float flake = float.MaxValue;
            for (int arm = 0; arm < 6; arm++)
            {
                float angle = arm * Mathf.PI / 3f + Mathf.PI / 2f;
                float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
                flake = Mathf.Min(flake, Segment(x, y, 0f, 0f, dx * 0.62f, dy * 0.62f, 0.04f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float branch = angle + side * 0.8f;
                    flake = Mathf.Min(flake, Segment(x, y, dx * 0.34f, dy * 0.34f,
                        dx * 0.34f + Mathf.Cos(branch) * 0.2f, dy * 0.34f + Mathf.Sin(branch) * 0.2f, 0.035f));
                }
            }
            return Over(c, FreezeColor, Fill(flake));
        }
    }
}
