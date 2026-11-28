using TMPro;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Colors a TMP text with the theme's title gradient from the left to the right edge of its rect
    /// (the splash and menu title). TMP gradients are per letter; this one runs across the whole rect,
    /// so two lines of equal width ("Colours" / "2048") continue the same gradient.
    /// The text's own color should be white: vertex colors are multiplied by it.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TextGradient2048 : MonoBehaviour
    {
        [SerializeField] Theme2048 theme;

        TMP_Text _text;

        void Awake() => _text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            _text.ForceMeshUpdate();
            Apply();
        }

        void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        // TMP rebuilds the mesh (and resets the colors) after any text or layout change.
        void OnTextChanged(Object changed)
        {
            if (changed == _text) Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_text != null && isActiveAndEnabled) Apply();
        }

        void Apply()
        {
            if (theme == null) return;
            var info = _text.textInfo;
            var rect = _text.rectTransform.rect;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var mesh = info.meshInfo[ch.materialReferenceIndex];
                for (int k = 0; k < 4; k++)
                {
                    float t = Mathf.InverseLerp(rect.xMin, rect.xMax, mesh.vertices[ch.vertexIndex + k].x);
                    mesh.colors32[ch.vertexIndex + k] = theme.TitleColor(t);
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
