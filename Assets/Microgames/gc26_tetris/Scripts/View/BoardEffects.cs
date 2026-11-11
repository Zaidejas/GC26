using System.Collections.Generic;
using UnityEngine;

namespace GC26.Tetris.View
{
    /// <summary>Short-lived visuals over the board: shards, streaks, rings and camera shake.</summary>
    public sealed class BoardEffects
    {
        const int MaxPieces = 420;
        const int SortOrder = 6;

        sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Vector2 Position, Velocity;
            public Vector2 ScaleFrom, ScaleTo;
            public float Gravity, Spin, Angle, Life, MaxLife;
            public Color Color;
        }

        readonly Transform root;
        readonly Material material;
        readonly Sprite shardSprite;
        readonly Sprite plainSprite;
        readonly Sprite ringSprite;
        readonly List<Piece> active = new List<Piece>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        readonly System.Random random = new System.Random(2024);
        float shake;

        /// <summary>Offset for the camera this frame.</summary>
        public Vector2 ShakeOffset { get; private set; }

        public BoardEffects(Transform parent, Material spriteMaterial, Sprite shard, Sprite plain)
        {
            root = new GameObject("BoardEffects").transform;
            root.SetParent(parent, false);
            material = spriteMaterial;
            shardSprite = shard;
            plainSprite = plain;
            ringSprite = CreateRingSprite();
        }

        public void Shake(float amount)
        {
            shake = Mathf.Min(1f, Mathf.Max(shake, amount));
        }

        /// <summary>Shards flying out of one cell.</summary>
        public void Burst(Vector2 center, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) + 0.4f) * (speed * Range(0.35f, 1f));
                float size = Range(0.16f, 0.36f);
                Spawn(shardSprite, center, velocity, 16f, Range(-420f, 420f), Range(0.4f, 0.8f),
                    new Vector2(size, size), new Vector2(0.03f, 0.03f), color);
            }
        }

        /// <summary>A fading vertical streak behind a hard-dropped cell.</summary>
        public void Streak(float x, float fromY, float toY, Color color)
        {
            float length = fromY - toY;
            if (length < 0.5f)
                return;
            Vector2 center = new Vector2(x, (fromY + toY) / 2f);
            color.a = 0.5f;
            Spawn(plainSprite, center, Vector2.zero, 0f, 0f, 0.22f,
                new Vector2(0.84f, length), new Vector2(0.15f, length), color);
        }

        /// <summary>A bright beam along a whole column of the well.</summary>
        public void Beam(float x, float centerY, float height, Color color)
        {
            Spawn(plainSprite, new Vector2(x, centerY), Vector2.zero, 0f, 0f, 0.4f,
                new Vector2(1.5f, height), new Vector2(0.12f, height), color);
        }

        public void Ring(Vector2 center, Color color, float endSize, float life)
        {
            Spawn(ringSprite, center, Vector2.zero, 0f, 0f, life,
                new Vector2(0.5f, 0.5f), new Vector2(endSize, endSize), color);
        }

        public void Tick(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Piece piece = active[i];
                piece.Life -= deltaTime;
                if (piece.Life <= 0f)
                {
                    piece.Renderer.gameObject.SetActive(false);
                    pool.Push(piece.Renderer);
                    active[i] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                    continue;
                }

                float t = 1f - piece.Life / piece.MaxLife;
                piece.Velocity.y -= piece.Gravity * deltaTime;
                piece.Position += piece.Velocity * deltaTime;
                piece.Angle += piece.Spin * deltaTime;
                Transform transform = piece.Renderer.transform;
                transform.localPosition = piece.Position;
                transform.localRotation = Quaternion.Euler(0f, 0f, piece.Angle);
                Vector2 scale = Vector2.Lerp(piece.ScaleFrom, piece.ScaleTo, t);
                transform.localScale = new Vector3(scale.x, scale.y, 1f);
                Color color = piece.Color;
                color.a *= 1f - t * t;
                piece.Renderer.color = color;
            }

            shake = Mathf.Max(0f, shake - deltaTime * 2.4f);
            float strength = shake * shake * 0.75f;
            float clock = Time.unscaledTime * 41f;
            ShakeOffset = new Vector2(Mathf.PerlinNoise(clock, 3.7f) - 0.5f, Mathf.PerlinNoise(9.1f, clock) - 0.5f) * (2f * strength);
        }

        void Spawn(Sprite sprite, Vector2 position, Vector2 velocity, float gravity, float spin, float life,
            Vector2 scaleFrom, Vector2 scaleTo, Color color)
        {
            if (active.Count >= MaxPieces)
                return;
            SpriteRenderer renderer = pool.Count > 0 ? pool.Pop() : CreateRenderer();
            renderer.gameObject.SetActive(true);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.transform.localPosition = position;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(scaleFrom.x, scaleFrom.y, 1f);
            active.Add(new Piece
            {
                Renderer = renderer,
                Position = position,
                Velocity = velocity,
                Gravity = gravity,
                Spin = spin,
                Life = life,
                MaxLife = life,
                ScaleFrom = scaleFrom,
                ScaleTo = scaleTo,
                Color = color,
            });
        }

        SpriteRenderer CreateRenderer()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = SortOrder;
            return renderer;
        }

        float Range(float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        static Sprite CreateRingSprite()
        {
            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                    float distance = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 0.88f) - 0.06f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance * Size / 2f)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
        }
    }
}
