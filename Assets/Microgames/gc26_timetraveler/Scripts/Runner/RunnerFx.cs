using System.Collections.Generic;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Tiny square particles for feel (dust when running, jumping and landing, coin sparkles,
    /// a burst on world switch and death). Plain coloured squares from a fixed pool: no art needed.
    /// Uses game time, so time slow slows them too.
    /// </summary>
    public class RunnerFx : MonoBehaviour
    {
        const int PoolSize = 96;
        const float Gravity = 18f;

        class Bit
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life, MaxLife, Size;
            public Color Color;
            public bool Falls;
        }

        readonly List<Bit> _bits = new List<Bit>();
        int _next;

        public void Init(RunnerShapes shapes)
        {
            for (var i = 0; i < PoolSize; i++)
            {
                var sr = shapes.Visual(transform, "FxBit", Vector2.one * 0.3f, Color.white, 4);
                sr.gameObject.SetActive(false);
                _bits.Add(new Bit { Renderer = sr });
            }
        }

        /// <summary>Spawns <paramref name="count"/> bits around a point.</summary>
        /// <param name="direction">Main direction of travel (units/s); each bit gets some random spread.</param>
        public void Burst(Vector2 at, int count, Color color, Vector2 direction, float spread, float size, float life,
            bool falls = true)
        {
            for (var i = 0; i < count; i++)
            {
                var b = _bits[_next];
                _next = (_next + 1) % _bits.Count;
                b.Velocity = direction + Random.insideUnitCircle * spread;
                b.Life = b.MaxLife = life * Random.Range(0.7f, 1.2f);
                b.Size = size * Random.Range(0.7f, 1.2f);
                b.Color = color;
                b.Falls = falls;
                b.Renderer.transform.position = at + Random.insideUnitCircle * 0.2f;
                b.Renderer.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            var dt = Time.deltaTime;
            foreach (var b in _bits)
            {
                if (b.Life <= 0f) continue;
                b.Life -= dt;
                if (b.Life <= 0f)
                {
                    b.Renderer.gameObject.SetActive(false);
                    continue;
                }
                if (b.Falls) b.Velocity.y -= Gravity * dt;
                var t = b.Life / b.MaxLife;
                var tr = b.Renderer.transform;
                tr.position += (Vector3)(b.Velocity * dt);
                tr.localScale = Vector3.one * (b.Size * (0.4f + 0.6f * t));
                var c = b.Color;
                c.a *= t;
                b.Renderer.color = c;
            }
        }

        public void Clear()
        {
            foreach (var b in _bits)
            {
                b.Life = 0f;
                b.Renderer.gameObject.SetActive(false);
            }
        }
    }
}
