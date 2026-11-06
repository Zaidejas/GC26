using System.Collections.Generic;
using UnityEngine;

namespace BlockDrop.View
{
    /// <summary>Draws the bot in the right column and the coins that fly out of it.</summary>
    public sealed class BattleView
    {
        const int MaxCoins = 40;
        const float CoinSize = 0.55f;
        const float FlyStart = 0.6f;
        const float FlyTime = 0.45f;
        const float BotSize = 3.5f;
        const float HitTime = 0.28f;
        const float AppearTime = 0.5f;

        // Each new bot gets its own colour.
        static readonly Color[] BotColors =
        {
            new Color(0.62f, 0.72f, 0.86f),
            new Color(0.95f, 0.66f, 0.36f),
            new Color(0.56f, 0.82f, 0.56f),
            new Color(0.88f, 0.55f, 0.78f),
            new Color(0.95f, 0.84f, 0.42f),
            new Color(0.62f, 0.6f, 0.95f),
        };

        static readonly string[] BotNames = { "Жестянка", "Гайка", "Болтун", "Шестерня", "Ржавчик", "Паяльник" };

        sealed class Coin
        {
            public SpriteRenderer Renderer;
            public Vector2 Position, Velocity, FlyFrom;
            public float Clock, Spin;
        }

        readonly GameObject root;
        readonly Transform parent;
        readonly Material material;
        readonly SpriteRenderer panel;
        readonly SpriteRenderer body;
        readonly Sprite coinSprite;
        readonly BoardEffects effects;
        readonly List<Coin> coins = new List<Coin>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        readonly System.Random random = new System.Random(77);
        Color tint = BotColors[0];
        float hitTimer, appearTimer, clock;

        public Vector2 BotCenter { get; }
        public Vector2 CoinTarget { get; }
        public Sprite CoinSprite { get { return coinSprite; } }

        /// <summary>Raised when a coin reaches the counter.</summary>
        public event System.Action CoinArrived;

        public BattleView(Transform parent, Material material, Sprite plainSprite, Color panelColor, BoardEffects effects, Vector2 botCenter, Vector2 coinTarget, Vector2 panelCenter, Vector2 panelSize)
        {
            this.parent = parent;
            this.material = material;
            this.effects = effects;
            BotCenter = botCenter;
            CoinTarget = coinTarget;
            coinSprite = CreateCoinSprite();

            root = new GameObject("BattleView");
            root.transform.SetParent(parent, false);

            panel = NewRenderer("BotPanel", plainSprite, 0);
            panel.transform.localPosition = panelCenter;
            panel.transform.localScale = new Vector3(panelSize.x, panelSize.y, 1f);
            panel.color = panelColor;

            body = NewRenderer("Bot", CreateBotSprite(), 5);
            root.SetActive(false);
        }

        public static string NameFor(int level)
        {
            return BotNames[(level - 1) % BotNames.Length];
        }

        public void Show(bool visible)
        {
            root.SetActive(visible);
            foreach (Coin coin in coins)
                Release(coin.Renderer);
            coins.Clear();
            if (visible)
                BotAppears(1);
        }

        public void SetPanelColor(Color color)
        {
            panel.color = color;
        }

        public void BotHit(float strength)
        {
            hitTimer = HitTime * Mathf.Clamp(strength, 0.5f, 1.5f);
        }

        public void BotDefeated(int nextLevel)
        {
            effects.Burst(BotCenter, tint, 44, 13f);
            effects.Burst(BotCenter, Color.white, 14, 9f);
            effects.Ring(BotCenter, new Color(tint.r, tint.g, tint.b, 0.6f), 11f, 0.45f);
            BotAppears(nextLevel);
        }

        void BotAppears(int level)
        {
            tint = BotColors[(level - 1) % BotColors.Length];
            appearTimer = AppearTime;
            hitTimer = 0f;
        }

        public void DropCoins(int count)
        {
            for (int i = 0; i < count && coins.Count < MaxCoins; i++)
            {
                SpriteRenderer renderer = pool.Count > 0 ? pool.Pop() : NewRenderer("Coin", coinSprite, 7);
                renderer.gameObject.SetActive(true);
                float angle = Range(0.25f, 0.75f) * Mathf.PI;
                float speed = Range(5f, 10f);
                coins.Add(new Coin
                {
                    Renderer = renderer,
                    Position = BotCenter + new Vector2(Range(-0.8f, 0.8f), Range(-0.4f, 0.8f)),
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    // Staggered, so a big drop pours out instead of moving as one lump.
                    Clock = -i * 0.03f,
                    Spin = Range(6f, 12f),
                });
            }
        }

        public void Tick(float deltaTime)
        {
            clock += deltaTime;
            hitTimer = Mathf.Max(0f, hitTimer - deltaTime);
            appearTimer = Mathf.Max(0f, appearTimer - deltaTime);
            UpdateBot();

            for (int i = coins.Count - 1; i >= 0; i--)
            {
                Coin coin = coins[i];
                coin.Clock += deltaTime;
                if (coin.Clock < 0f)
                {
                    coin.Renderer.enabled = false;
                    continue;
                }
                coin.Renderer.enabled = true;

                Vector2 position;
                if (coin.Clock < FlyStart)
                {
                    coin.Velocity.y -= 22f * deltaTime;
                    coin.Position += coin.Velocity * deltaTime;
                    coin.FlyFrom = coin.Position;
                    position = coin.Position;
                }
                else
                {
                    float t = (coin.Clock - FlyStart) / FlyTime;
                    if (t >= 1f)
                    {
                        Release(coin.Renderer);
                        coins[i] = coins[coins.Count - 1];
                        coins.RemoveAt(coins.Count - 1);
                        CoinArrived?.Invoke();
                        continue;
                    }
                    position = Vector2.Lerp(coin.FlyFrom, CoinTarget, t * t);
                }

                Transform transform = coin.Renderer.transform;
                transform.localPosition = position;
                // A narrowing width reads as the coin spinning on its edge.
                float turn = Mathf.Abs(Mathf.Cos(coin.Clock * coin.Spin));
                transform.localScale = new Vector3(CoinSize * Mathf.Lerp(0.25f, 1f, turn), CoinSize, 1f);
            }
        }

        void UpdateBot()
        {
            float hit = hitTimer / HitTime;
            float appear = 1f - appearTimer / AppearTime;
            float grow = Mathf.SmoothStep(0f, 1f, appear) * (1f + 0.15f * Mathf.Sin(appear * Mathf.PI));
            float breathe = 1f + 0.02f * Mathf.Sin(clock * 3f);

            Vector2 offset = new Vector2(0f, 0.08f * Mathf.Sin(clock * 2f));
            if (hit > 0f)
                offset += new Vector2(Mathf.Sin(clock * 70f) * 0.28f * hit, Mathf.Cos(clock * 55f) * 0.12f * hit);

            Transform transform = body.transform;
            transform.localPosition = BotCenter + offset;
            float size = BotSize * grow;
            transform.localScale = new Vector3(size * (breathe + 0.14f * hit), size * (2f - breathe - 0.14f * hit), 1f);
            body.color = Color.Lerp(tint, new Color(1f, 0.25f, 0.2f), Mathf.Clamp01(hit * 1.1f));
        }

        float Range(float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        void Release(SpriteRenderer renderer)
        {
            renderer.gameObject.SetActive(false);
            pool.Push(renderer);
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        // --- sprites ----------------------------------------------------------------------

        delegate Color Painter(float x, float y);

        static Sprite Build(int size, Painter paint)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f;
                    float y = (j + 0.5f) / size * 2f - 1f;
                    texture.SetPixel(i, j, paint(x, y));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        static float Fill(float distance, float softness = 0.025f)
        {
            return Mathf.Clamp01(0.5f - distance / softness);
        }

        static float Circle(float x, float y, float cx, float cy, float r)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float round)
        {
            float qx = Mathf.Abs(x - cx) - hw + round, qy = Mathf.Abs(y - cy) - hh + round;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
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

        // A square little robot, painted in light greys so the renderer colour can tint it.
        static Sprite CreateBotSprite()
        {
            Color dark = new Color(0.16f, 0.14f, 0.2f);
            return Build(192, (x, y) =>
            {
                Color c = new Color(1f, 1f, 1f, 0f);
                // Antenna.
                c = Over(c, new Color(0.7f, 0.7f, 0.72f), Fill(Box(x, y, 0f, 0.72f, 0.035f, 0.15f, 0.02f)));
                c = Over(c, Color.white, Fill(Circle(x, y, 0f, 0.87f, 0.09f)));
                // Ears.
                c = Over(c, new Color(0.55f, 0.55f, 0.58f), Fill(Box(x, y, -0.82f, 0.1f, 0.1f, 0.2f, 0.05f)));
                c = Over(c, new Color(0.55f, 0.55f, 0.58f), Fill(Box(x, y, 0.82f, 0.1f, 0.1f, 0.2f, 0.05f)));
                // Head, lit from above.
                float head = Box(x, y, 0f, 0.08f, 0.74f, 0.62f, 0.16f);
                float shade = Mathf.Lerp(0.72f, 1f, (y + 0.6f) / 1.3f);
                c = Over(c, new Color(shade, shade, shade), Fill(head));
                // Face plate and eyes.
                c = Over(c, dark, Fill(Box(x, y, 0f, 0.2f, 0.56f, 0.27f, 0.12f)));
                c = Over(c, new Color(0.55f, 1f, 0.95f), Fill(Circle(x, y, -0.27f, 0.2f, 0.13f)));
                c = Over(c, new Color(0.55f, 1f, 0.95f), Fill(Circle(x, y, 0.27f, 0.2f, 0.13f)));
                c = Over(c, Color.white, Fill(Circle(x, y, -0.23f, 0.24f, 0.05f)));
                c = Over(c, Color.white, Fill(Circle(x, y, 0.31f, 0.24f, 0.05f)));
                // Mouth: a row of teeth.
                c = Over(c, dark, Fill(Box(x, y, 0f, -0.28f, 0.34f, 0.09f, 0.03f)));
                float teeth = Mathf.Abs(Mathf.Repeat(x + 1f, 0.17f) - 0.085f) - 0.05f;
                c = Over(c, new Color(0.9f, 0.9f, 0.9f), Fill(Mathf.Max(teeth, Box(x, y, 0f, -0.28f, 0.31f, 0.06f, 0.02f))));
                // Neck and shoulders.
                c = Over(c, new Color(0.5f, 0.5f, 0.53f), Fill(Box(x, y, 0f, -0.62f, 0.2f, 0.09f, 0.02f)));
                c = Over(c, new Color(0.8f, 0.8f, 0.82f), Fill(Box(x, y, 0f, -0.82f, 0.55f, 0.14f, 0.08f)));
                return c;
            });
        }

        static Sprite CreateCoinSprite()
        {
            Color gold = new Color(1f, 0.8f, 0.25f);
            return Build(64, (x, y) =>
            {
                Color c = new Color(gold.r, gold.g, gold.b, 0f);
                float r = Mathf.Sqrt(x * x + y * y);
                c = Over(c, new Color(0.72f, 0.46f, 0.08f), Fill(r - 0.94f, 0.06f));
                c = Over(c, gold, Fill(r - 0.8f, 0.06f));
                c = Over(c, new Color(1f, 0.93f, 0.55f), Fill(Mathf.Abs(r - 0.56f) - 0.06f, 0.06f));
                c = Over(c, new Color(0.82f, 0.56f, 0.12f), Fill(Box(x, y, 0f, 0f, 0.1f, 0.34f, 0.04f), 0.06f));
                return c;
            });
        }
    }
}
