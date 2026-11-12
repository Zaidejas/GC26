using System.Collections.Generic;
using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// Fireflies on the game screen (teacher's feedback; prototype's .fly): 12 glowing dots in warm gold and soft
    /// turquoise, spread over the screen behind the board. Each drifts slowly back and forth (7–14 s) and pulses
    /// brighter and dimmer (1.5–4 s), at its own pace. They belong to the board, so they show only with it.
    /// </summary>
    public sealed class Fireflies : MonoBehaviour
    {
        public const int Count = 12;
        public const int SortingOrder = -10; // behind the board's glow, in front of the forest background
        static readonly Color[] Colors = { Palette.Hex(0xFFD56B), Palette.Hex(0xFFE9A8), Palette.Hex(0x8FF0E0) };

        sealed class Fly
        {
            public Transform Root;
            public SpriteRenderer Glow, Core;
            public Vector2 Spot;          // where on the screen, 0–1 from the bottom-left corner
            public Vector2 Drift;         // how far it wanders, in units (1 % of the content width)
            public float Size, DriftSeconds, PulseSeconds, DriftOffset, PulseOffset;
            public Color Color;
        }

        readonly List<Fly> flies = new List<Fly>();
        Vector2 screenMin, screenSize;    // the screen in the board's own units
        float unitWorld = 0.1f;           // 1 unit (1 % of the content width) in the board's units
        float clock;

        public int FlyCount => flies.Count;

        public static Fireflies Create(Transform board)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(board, false);
            var view = go.AddComponent<Fireflies>();
            view.Build();
            return view;
        }

        void Build()
        {
            for (int k = 0; k < Count; k++)
            {
                var fly = new Fly
                {
                    Spot = new Vector2(0.05f + Random.value * 0.9f, 0.05f + Random.value * 0.9f),
                    Drift = new Vector2((Random.value - 0.5f) * 24f, (Random.value - 0.5f) * 18f),
                    Size = 1.6f + Random.value * 1.6f,
                    DriftSeconds = 7f + Random.value * 7f,
                    PulseSeconds = 1.5f + Random.value * 2.5f,
                    DriftOffset = Random.value * 10f,
                    PulseOffset = Random.value * 4f,
                    Color = Colors[k % Colors.Length],
                };
                fly.Root = new GameObject("Firefly " + k).transform;
                fly.Root.SetParent(transform, false);
                fly.Glow = NewSprite("Glow", fly.Root, SpriteFactory.SoftDisc(), SortingOrder);
                fly.Core = NewSprite("Core", fly.Root, SpriteFactory.Disc(), SortingOrder + 1);
                fly.Core.transform.localScale = Vector3.one * 0.25f;
                flies.Add(fly);
            }
        }

        /// <summary>Spreads the fireflies over the screen for <paramref name="layout"/> (the board sits at its centre).</summary>
        public void ApplyLayout(BoardLayout layout)
        {
            var centre = layout.BoardCenterWorld;
            screenMin = layout.ScreenToWorld(Vector2.zero) - centre;
            screenSize = layout.ScreenToWorld(layout.ScreenSize) - centre - screenMin;
            unitWorld = layout.Unit / layout.CellPitch;
            Advance(0f);
        }

        void Update()
        {
            if (Application.isPlaying) Advance(Time.deltaTime);
        }

        /// <summary>Moves the fireflies on by <paramref name="seconds"/> (every frame; tests call it directly).</summary>
        public void Advance(float seconds)
        {
            clock += seconds;
            foreach (var fly in flies)
            {
                float drift = Easing.Evaluate(Ease.InOutSine, Mathf.PingPong((clock + fly.DriftOffset) / fly.DriftSeconds, 1f));
                float pulse = Easing.Evaluate(Ease.InOutSine, Mathf.PingPong((clock + fly.PulseOffset) / fly.PulseSeconds, 1f));
                var spot = screenMin + Vector2.Scale(fly.Spot, screenSize) + fly.Drift * unitWorld * drift;
                fly.Root.localPosition = new Vector3(spot.x, spot.y, 0f);
                fly.Root.localScale = Vector3.one * (fly.Size * unitWorld);
                float alpha = Mathf.Lerp(0.15f, 0.9f, pulse);
                fly.Glow.color = new Color(fly.Color.r, fly.Color.g, fly.Color.b, alpha);
                fly.Core.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        /// <summary>A firefly's brightness now, 0–1 (tests).</summary>
        public float Brightness(int index) => flies[index].Glow.color.a;

        /// <summary>A firefly's position in the board's units (tests).</summary>
        public Vector3 Position(int index) => flies[index].Root.localPosition;

        static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
