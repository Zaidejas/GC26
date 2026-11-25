using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Simple white shapes drawn at runtime (circle, rounded square, ring), tinted by SpriteRenderer/Image color.
    /// They stand in for art until W5. All sprites are 1 world unit wide.
    /// </summary>
    public class PlaceholderArt : MonoBehaviour
    {
        const int Size = 64;

        Sprite _circle;
        Sprite _square;
        Sprite _ring;
        Sprite _rect;
        readonly List<Object> _owned = new();

        /// <summary>A plain square with sharp corners (bars).</summary>
        public Sprite Rect => _rect != null ? _rect : _rect = Make((x, y) => 1f);

        Sprite _star;

        /// <summary>A five-pointed star (level ratings).</summary>
        public Sprite Star => _star != null ? _star : _star = Make((x, y) =>
        {
            // 4x4 samples per pixel for smooth edges.
            int inside = 0;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
                if (InStar((x - 0.5f + (sx + 0.5f) / 4f) / Size, (y - 0.5f + (sy + 0.5f) / 4f) / Size)) inside++;
            return inside / 16f;
        });

        static readonly Vector2[] StarPoints = BuildStar();

        static Vector2[] BuildStar()
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.5f : 0.21f;
                points[i] = new Vector2(0.5f + Mathf.Cos(angle) * r, 0.47f + Mathf.Sin(angle) * r);
            }
            return points;
        }

        /// <summary>Even-odd test against the star outline, in 0..1 coordinates.</summary>
        static bool InStar(float px, float py)
        {
            bool inside = false;
            var p = StarPoints;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > py) != (p[j].y > py) &&
                    px < (p[j].x - p[i].x) * (py - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        public Sprite Circle => _circle != null ? _circle : _circle = Make((x, y) => Mathf.Clamp01(Size / 2f - Dist(x, y)));

        public Sprite Square => _square != null ? _square : _square = Make((x, y) =>
        {
            // Rounded square: corner radius 8 px.
            const float r = 8f;
            float dx = Mathf.Max(Mathf.Abs(x - Size / 2f) - (Size / 2f - r), 0f);
            float dy = Mathf.Max(Mathf.Abs(y - Size / 2f) - (Size / 2f - r), 0f);
            return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
        });

        /// <summary>A thin circle outline, for range previews and splash flashes.</summary>
        public Sprite Ring => _ring != null ? _ring : _ring = Make((x, y) =>
        {
            float d = Dist(x, y);
            return Mathf.Clamp01(Size / 2f - d) * Mathf.Clamp01(d - (Size / 2f - 3f));
        });

        static float Dist(float x, float y) => Vector2.Distance(new Vector2(x, y), new Vector2(Size / 2f, Size / 2f));

        Sprite Make(System.Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alphaAt(x + 0.5f, y + 0.5f) * 255));
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>A plain colored sprite object, e.g. a slot, a projectile or a flash.</summary>
        public SpriteRenderer NewShape(string name, Sprite sprite, Transform parent, Vector3 position, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A short-lived straight line between two points (shots, chains).</summary>
        public void FlashLine(Vector3 from, Vector3 to, Color color, float width, float seconds, Transform parent)
        {
            Vector3 d = to - from;
            var sr = NewShape("Flash", Square, parent, (from + to) / 2f, 1f, color, 31);
            sr.transform.localScale = new Vector3(d.magnitude, width, 1f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            Destroy(sr.gameObject, seconds);
        }

        /// <summary>A short-lived ring (splash).</summary>
        public void FlashRing(Vector3 center, float radius, Color color, float seconds, Transform parent)
        {
            var sr = NewShape("Splash", Ring, parent, center, radius * 2f, color, 31);
            Destroy(sr.gameObject, seconds);
        }

        void OnDestroy()
        {
            foreach (var o in _owned)
                if (o != null) Destroy(o);
        }
    }
}
