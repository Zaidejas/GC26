using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>One tile on screen (prefab gc26_2048_nastya_Tile). Only displays; never changes game state.</summary>
    public sealed class TileView2048 : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;

        public RectTransform Rect => (RectTransform)transform;
        public int Value { get; private set; }

        public void Set(int value, Theme2048 theme, float tileSize)
        {
            var style = theme.StyleFor(value);
            background.color = style.tile;
            label.color = style.text;
            label.fontSize = theme.FontSizeFor(value, tileSize);
            if (Value != value) label.SetText(theme.TextFor(value));
            Value = value;
        }

        public void Place(Vector2 position, float size)
        {
            var rect = Rect;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.one;
        }
    }
}
