using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// One tile on screen (prefab gc26_2048_nastya_Tile). Only displays; never changes game state.
    /// High tiles get a soft glow behind them and a light outline (GDD 5.2, also for color-blind players);
    /// 2048 and above pulse their glow.
    /// </summary>
    public sealed class TileView2048 : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;

        [Header("Glow and outline (T51), optional")]
        [SerializeField] Image glow;
        [SerializeField] Outline outline;
        [SerializeField] float glowAlpha = 0.75f;
        [SerializeField] float outlineAlpha = 0.6f;
        [SerializeField] float outlineWidth = 5f;
        [SerializeField] float pulsePeriod = 1.2f;
        [SerializeField, Range(0f, 1f)] float pulseDepth = 0.45f;

        public RectTransform Rect => (RectTransform)transform;
        public int Value { get; private set; }

        /// <summary>Glow strength from the theme, 0..1 (0 = no glow).</summary>
        public float Glow { get; private set; }
        public bool IsPulsing => enabled && Glow > 0f && Value >= GameState2048.WinValue;

        Color _glowColor;

        void Awake() => enabled = false; // Update runs only for pulsing tiles

        public void Set(int value, Theme2048 theme, float tileSize)
        {
            var style = theme.StyleFor(value);
            background.color = style.tile;
            label.color = style.text;
            label.fontSize = theme.FontSizeFor(value, tileSize);
            if (Value != value) label.SetText(theme.TextFor(value));
            Value = value;

            Glow = style.glow;
            if (glow != null)
            {
                glow.enabled = Glow > 0f;
                _glowColor = style.tile;
                _glowColor.a = Glow * glowAlpha;
                glow.color = _glowColor;
            }
            if (outline != null)
            {
                outline.enabled = Glow > 0f;
                outline.effectColor = new Color(1f, 1f, 1f, Glow * outlineAlpha);
                float w = outlineWidth * Glow * tileSize / Theme2048.ReferenceTileSize;
                outline.effectDistance = new Vector2(w, -w);
            }
            enabled = glow != null && Glow > 0f && value >= GameState2048.WinValue;
        }

        public void Place(Vector2 position, float size)
        {
            var rect = Rect;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.one;
        }

        void Update()
        {
            if (glow == null) { enabled = false; return; } // an older prefab without the glow layer
            // 2048+: the glow breathes. Unscaled time, like every animation in this game.
            float k = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / pulsePeriod));
            var c = _glowColor;
            c.a *= k;
            glow.color = c;
        }

        void OnDisable()
        {
            if (glow != null) glow.color = _glowColor;
        }
    }
}
