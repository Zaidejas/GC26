using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Findepair
{
    /// <summary>
    /// Buttons shrink a little while pressed. With <see cref="pulse"/> on they also "breathe" to draw
    /// attention: a slow scale swell and a brighter neon glow, staggered so a column of buttons ripples.
    /// </summary>
    public class FindepairPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float PulsePeriod = 1.8f;
        public const float PulseScale = 0.035f;

        public bool pulse;

        bool _pressed;
        Image _glow;
        Color _glowBase;

        void Start()
        {
            var g = transform.Find("Glow");
            if (g != null && g.TryGetComponent(out _glow)) _glowBase = _glow.color;
        }

        public void OnPointerDown(PointerEventData e) => _pressed = true;
        public void OnPointerUp(PointerEventData e) => _pressed = false;
        public void OnPointerExit(PointerEventData e) => _pressed = false;

        void OnDisable()
        {
            _pressed = false;
            transform.localScale = Vector3.one;
            if (_glow != null) _glow.color = _glowBase;
        }

        void Update()
        {
            float wave = 0f;
            if (pulse)
            {
                // 0..1, eased; each button lags the one above it a little.
                float phase = Time.unscaledTime / PulsePeriod * Mathf.PI * 2f - transform.GetSiblingIndex() * 0.6f;
                wave = 0.5f - 0.5f * Mathf.Cos(phase);
                if (_glow != null)
                    _glow.color = FindepairUIKit.Alpha(_glowBase, Mathf.Clamp01(_glowBase.a * (0.6f + 0.8f * wave)));
            }
            float scale = (_pressed ? 0.96f : 1f) * (1f + PulseScale * wave);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
