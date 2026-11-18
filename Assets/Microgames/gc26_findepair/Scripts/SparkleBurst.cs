using System;
using UnityEngine;
using UnityEngine.UI;
using static GC26.Findepair.FindepairUIKit;

namespace GC26.Findepair
{
    /// <summary>
    /// Bright pink sparkle burst when a pair vanishes. ParticleSystem doesn't draw on an Overlay canvas,
    /// so the sparkles are pooled UI Images moved in Update (on game time, so they freeze while paused).
    /// </summary>
    public class SparkleBurst : MonoBehaviour
    {
        public const int SparksPerCard = 16;
        public const int PoolSize = 96;   // up to 3 pairs at once x 2 cards x 16 sparkles
        const int FlashPool = 6;
        const float FlashSeconds = 0.32f;

        static readonly Color[] Colors =
        {
            Hex("#FF4FC4"), Hex("#FF8FE0"), Hex("#FFC2F0"), Hex("#FF4FC4"), Color.white,
        };

        class Spark
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 From, Offset;
            public float Age, Life, Size, Spin;
            public bool Alive;
        }

        class Flash
        {
            public RectTransform Rt;
            public Image Img;
            public float Age, Size;
            public bool Alive;
        }

        readonly Spark[] _sparks = new Spark[PoolSize];
        readonly Flash[] _flashes = new Flash[FlashPool];
        int _nextSpark, _nextFlash;
        Func<float> _delta = () => Time.unscaledDeltaTime;
        readonly System.Random _rng = new System.Random();

        public int ActiveCount { get; private set; }

        public void Init(Sprite sparkle, Sprite flash, Func<float> delta)
        {
            _delta = delta;
            for (int i = 0; i < FlashPool; i++)
            {
                var img = Img("Flash", transform, flash, Color.white);
                img.gameObject.SetActive(false);
                _flashes[i] = new Flash { Rt = img.rectTransform, Img = img };
            }
            for (int i = 0; i < PoolSize; i++)
            {
                var img = Img("Spark", transform, sparkle, Color.white);
                img.gameObject.SetActive(false);
                _sparks[i] = new Spark { Rt = img.rectTransform, Img = img };
            }
        }

        /// <summary>Burst centered on a card (world position) of the given width (local units).</summary>
        public void Play(Vector3 worldCenter, float cardWidth)
        {
            Vector2 c = transform.InverseTransformPoint(worldCenter);

            var f = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % FlashPool;
            f.Age = 0;
            f.Size = cardWidth * 1.3f;
            f.Alive = true;
            f.Rt.anchoredPosition = c;
            f.Rt.anchorMin = f.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            f.Img.gameObject.SetActive(true);
            f.Rt.SetAsLastSibling();

            float reach = cardWidth * 1.1f;
            for (int i = 0; i < SparksPerCard; i++)
            {
                var s = _sparks[_nextSpark]; // when the pool runs out, the oldest sparkle is reused
                _nextSpark = (_nextSpark + 1) % PoolSize;
                float angle = i / (float)SparksPerCard * Mathf.PI * 2f + R(0f, 0.5f);
                float dist = reach * R(0.45f, 1.15f);
                s.From = c;
                // UI y points up: the slight "gravity" pulls sparkles down.
                s.Offset = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist - reach * 0.25f);
                s.Age = 0;
                s.Life = R(0.5f, 0.7f);
                s.Size = cardWidth * R(0.3f, 0.6f);
                s.Spin = R(-180f, 180f);
                s.Alive = true;
                s.Img.color = Colors[i % Colors.Length];
                s.Rt.anchorMin = s.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                s.Rt.anchoredPosition = c;
                s.Img.gameObject.SetActive(true);
                s.Rt.SetAsLastSibling();
            }
            Update();
        }

        public void Clear()
        {
            foreach (var s in _sparks) if (s != null) { s.Alive = false; s.Img.gameObject.SetActive(false); }
            foreach (var f in _flashes) if (f != null) { f.Alive = false; f.Img.gameObject.SetActive(false); }
            ActiveCount = 0;
        }

        void Update()
        {
            float d = _delta();
            int active = 0;
            foreach (var f in _flashes)
            {
                if (f == null || !f.Alive) continue;
                f.Age += d;
                float t = Mathf.Clamp01(f.Age / FlashSeconds);
                float e = EaseOut(t);
                f.Rt.sizeDelta = Vector2.one * f.Size * Mathf.Lerp(0.3f, 1.5f, e);
                f.Img.color = Alpha(Color.white, 1f - t);
                if (t >= 1f) { f.Alive = false; f.Img.gameObject.SetActive(false); } else active++;
            }
            foreach (var s in _sparks)
            {
                if (s == null || !s.Alive) continue;
                s.Age += d;
                float t = Mathf.Clamp01(s.Age / s.Life);
                float e = EaseOut(t);
                s.Rt.anchoredPosition = s.From + s.Offset * e;
                s.Rt.localRotation = Quaternion.Euler(0, 0, s.Spin * e);
                s.Rt.sizeDelta = Vector2.one * s.Size * Mathf.Lerp(1.3f, 0f, t);
                var col = s.Img.color;
                col.a = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                s.Img.color = col;
                if (t >= 1f) { s.Alive = false; s.Img.gameObject.SetActive(false); } else active++;
            }
            ActiveCount = active;
        }

        float R(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
