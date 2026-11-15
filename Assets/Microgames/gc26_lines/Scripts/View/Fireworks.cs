using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// L04: fireworks over the game screen when the score passes the best (prototype's <c>fireworks()</c>):
    /// 5 bursts in the upper half of the screen, 0–1 s apart, each 42 sparks in two of the palette's colours that
    /// fly out, slow down, fall a little and fade in about 0.9–1.4 s. The sparks are made once and reused, and the
    /// frame loop makes no garbage.
    /// </summary>
    public sealed class Fireworks : MonoBehaviour
    {
        public const int Bursts = 5, SparksPerBurst = 42;
        /// <summary>Drawn over the mushrooms, under the HUD and pop-ups (canvas order 20+).</summary>
        public const int Order = BoardView.BallOrder + 10;

        // Where (share of the screen's width and height from the top-left) and when (seconds) each burst goes off.
        static readonly Vector3[] Plan = { new Vector3(0.3f, 0.3f, 0f), new Vector3(0.7f, 0.24f, 0.25f), new Vector3(0.5f, 0.42f, 0.5f),
                                           new Vector3(0.22f, 0.5f, 0.8f), new Vector3(0.78f, 0.48f, 1.0f) };
        static readonly Color[] Colors = { Hex(0xFFD56B), Hex(0xFFE9A8), Hex(0x7FF0E0), Hex(0xFF8FC8), Hex(0xB9A2FF) };

        struct Spark { public Vector2 Position, Velocity; public float Age, Life; public Color Color; }

        readonly Spark[] sparks = new Spark[Bursts * SparksPerBurst];
        SpriteRenderer[] cores, glows;
        Camera view;
        float time, unit; // unit: 1/100 of the screen's width, in world units
        readonly bool[] fired = new bool[Bursts];
        readonly System.Random random = new System.Random();

        /// <summary>True while sparks are flying.</summary>
        public bool Playing { get; private set; }
        /// <summary>How many sparks are visible now (tests).</summary>
        public int VisibleSparks { get; private set; }

        public static Fireworks Create(Camera camera)
        {
            var go = new GameObject("Fireworks");
            var fireworks = go.AddComponent<Fireworks>();
            fireworks.view = camera;
            int count = Bursts * SparksPerBurst;
            fireworks.cores = new SpriteRenderer[count];
            fireworks.glows = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                fireworks.glows[i] = NewSprite("Spark glow", SpriteFactory.SoftDisc(), Order, go.transform);
                fireworks.cores[i] = NewSprite("Spark", SpriteFactory.Disc(), Order + 1, go.transform);
            }
            fireworks.Hide();
            return fireworks;
        }

        /// <summary>Starts the show (again from the start if it's already playing).</summary>
        public void Play()
        {
            float height = view.orthographicSize * 2f, width = height * view.aspect;
            unit = width / 100f;
            time = 0f;
            for (int i = 0; i < fired.Length; i++) fired[i] = false;
            for (int i = 0; i < sparks.Length; i++) sparks[i].Life = 0f;
            Playing = true;
            Advance(0f);
        }

        /// <summary>Stops at once and hides every spark (leaving the game screen).</summary>
        public void Stop()
        {
            Playing = false;
            Hide();
        }

        void Update()
        {
            if (Playing) Advance(Mathf.Min(Time.deltaTime, 0.04f));
        }

        /// <summary>Moves the show on by <paramref name="seconds"/> (tests call it directly).</summary>
        public void Advance(float seconds)
        {
            if (!Playing) return;
            time += seconds;
            Vector3 corner = view.transform.position;
            float height = view.orthographicSize * 2f, width = height * view.aspect;
            for (int b = 0; b < Bursts; b++)
            {
                if (fired[b] || time < Plan[b].z) continue;
                fired[b] = true;
                var at = new Vector2(corner.x - width / 2f + Plan[b].x * width, corner.y + height / 2f - Plan[b].y * height);
                Boom(b, at);
            }

            // The prototype's numbers, per 16 ms frame: speed × 0.985, falling 0.0016 unit/ms².
            float frames = seconds * 1000f / 16f, drag = Mathf.Pow(0.985f, frames);
            int visible = 0;
            for (int i = 0; i < sparks.Length; i++)
            {
                ref var s = ref sparks[i];
                if (s.Life <= 0f || s.Age >= s.Life)
                {
                    if (cores[i].enabled) { cores[i].enabled = false; glows[i].enabled = false; }
                    continue;
                }
                s.Age += seconds;
                s.Velocity *= drag;
                s.Velocity.y -= 0.0016f * unit * seconds * 1000f;
                s.Position += s.Velocity * frames;
                float f = 1f - Mathf.Clamp01(s.Age / s.Life);
                var core = cores[i]; var glow = glows[i];
                core.enabled = glow.enabled = f > 0f;
                if (f <= 0f) continue;
                visible++;
                core.transform.localPosition = s.Position;
                glow.transform.localPosition = s.Position;
                core.transform.localScale = Vector3.one * (0.5f + 0.6f * f) * unit;
                glow.transform.localScale = Vector3.one * 2.4f * f * unit; // a soft halo around each spark
                var c = s.Color; c.a = f; core.color = c;
                c.a = f * 0.3f; glow.color = c;
            }
            VisibleSparks = visible;
            bool waiting = false;
            for (int b = 0; b < Bursts; b++) waiting |= !fired[b];
            if (!waiting && visible == 0) Stop();
        }

        void Boom(int burst, Vector2 at)
        {
            Color c1 = Colors[random.Next(Colors.Length)], c2 = Colors[random.Next(Colors.Length)];
            for (int k = 0; k < SparksPerBurst; k++)
            {
                float a = k / (float)SparksPerBurst * Mathf.PI * 2f + (float)random.NextDouble() * 0.15f;
                float v = (0.55f + (float)random.NextDouble() * 0.5f) * unit * 0.42f; // per 16 ms frame
                ref var s = ref sparks[burst * SparksPerBurst + k];
                s.Position = at;
                s.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v;
                s.Age = 0f;
                s.Life = 0.9f + (float)random.NextDouble() * 0.5f;
                s.Color = k % 2 == 1 ? c1 : c2;
            }
        }

        void Hide()
        {
            VisibleSparks = 0;
            for (int i = 0; i < cores.Length; i++) { cores[i].enabled = false; glows[i].enabled = false; }
        }

        static SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.transform.SetParent(parent, false);
            sr.enabled = false;
            return sr;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
