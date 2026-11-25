using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static GC26.Findepair.FindepairUIKit;

namespace GC26.Findepair
{
    /// <summary>One card on the board: tap, flip, match pulse, miss shake and vanish.</summary>
    public class Card : MonoBehaviour
    {
        public CardData Data { get; private set; }
        public bool IsUp { get; private set; }
        public bool IsMatched { get; private set; }
        public bool IsGone { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        RectTransform _body;
        GameObject _back, _front;
        Image _frame, _glow, _shine;
        CanvasGroup _group;
        Button _button;
        Func<float> _delta;
        float _glowAlpha = 0.6f;

        /// <summary>Builds a face-down card under <paramref name="parent"/>.</summary>
        public static Card Create(Transform parent, CardData data, FindepairArt art, Func<float> delta, Action<Card> onClick)
        {
            var rt = Rect("Card " + data.id, parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var card = rt.gameObject.AddComponent<Card>();
            card.Data = data;
            card._delta = delta;
            card._group = rt.gameObject.AddComponent<CanvasGroup>();

            card._glow = Img("Glow", rt, art.glow, Alpha(Pink, card._glowAlpha), true, 0.9f);
            card._glow.rectTransform.Stretch(-34, -34, -34, -34);

            // Body is what flips; its rounded mask clips the pictures to the card shape.
            card._body = Rect("Body", rt).Stretch();
            var mask = card._body.gameObject.AddComponent<Image>();
            mask.sprite = art.rounded;
            mask.type = Image.Type.Sliced;
            mask.pixelsPerUnitMultiplier = 1f / 0.55f;
            mask.raycastTarget = false;
            card._body.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var back = Img("Back", card._body, art.cardBack, Color.white);
            back.rectTransform.Stretch();
            card._back = back.gameObject;

            var front = Img("Front", card._body, null, Bg);
            front.rectTransform.Stretch();
            var pic = Img("Art", front.transform, data.front, Color.white);
            pic.preserveAspect = true;
            pic.rectTransform.Stretch(6, 6, 6, 6);
            card._front = front.gameObject;
            card._front.SetActive(false);

            // Glint: a slanted light band that sweeps across the back while the board sits idle.
            card._shine = Img("Glint", card._body, null, new Color(1f, 1f, 1f, 0.38f));
            card._shine.rectTransform.localRotation = Quaternion.Euler(0, 0, -22f);
            card._shine.enabled = false;

            card._frame = Img("Frame", card._body, art.frame, Pink, true, 0.55f);
            card._frame.rectTransform.Stretch();

            // Push pin: the card is "pinned" to the cork board.
            var pin = Img("Pin", rt, art.circle, Pink);
            pin.rectTransform.anchorMin = pin.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            pin.rectTransform.sizeDelta = new Vector2(30, 30);
            pin.rectTransform.anchoredPosition = new Vector2(0, -2);
            var shine = Img("Shine", pin.transform, art.circle, new Color(1, 1, 1, 0.85f));
            shine.rectTransform.anchorMin = shine.rectTransform.anchorMax = new Vector2(0.35f, 0.65f);
            shine.rectTransform.sizeDelta = new Vector2(10, 10);

            card._button = rt.gameObject.AddComponent<Button>();
            card._button.transition = Selectable.Transition.None;
            card._button.targetGraphic = hit;
            card._button.onClick.AddListener(() => onClick(card));
            return card;
        }

        /// <summary>Flips the card over in FlipSeconds (scale X to 0, swap sides, back to 1).</summary>
        public IEnumerator Flip(bool up)
        {
            IsUp = up;
            Refresh();
            float half = FindepairBoard.FlipSeconds / 2f;
            yield return Animate(half, _delta, t => _body.localScale = new Vector3(1f - t, 1f, 1f));
            _back.SetActive(!up);
            _front.SetActive(up);
            var color = up ? Data.glow : Pink;
            _frame.color = color;
            _glow.color = Alpha(color, _glowAlpha);
            yield return Animate(half, _delta, t => _body.localScale = new Vector3(t, 1f, 1f));
        }

        /// <summary>Pair found: "pulse" and a bright neon flash.</summary>
        public void PlayMatch()
        {
            IsMatched = true;
            Refresh();
            StartCoroutine(Animate(0.3f, _delta, t => _body.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI))));
            StartCoroutine(Animate(0.7f, _delta, t =>
            {
                float k = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                _glow.color = Alpha(Color.Lerp(Data.glow, Pink, k), Mathf.Lerp(_glowAlpha, 1f, k));
                _glow.rectTransform.localScale = Vector3.one * (1f + 0.08f * k);
            }));
        }

        /// <summary>No match: a short shake.</summary>
        public void PlayMiss()
        {
            StartCoroutine(Animate(0.3f, _delta, t =>
                _body.localRotation = Quaternion.Euler(0, 0, 6f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t))));
        }

        /// <summary>Vanish: grow to 1.2 and fade out; the grid slot stays empty.</summary>
        public IEnumerator PlayVanish()
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
            yield return Animate(FindepairBoard.VanishSeconds, _delta, t =>
            {
                float e = t * t; // ease-in
                transform.localScale = Vector3.one * (1f + 0.2f * e);
                _group.alpha = 1f - e;
            });
            _group.alpha = 0f;
            IsGone = true;
        }

        /// <summary>Idle hint: after <paramref name="delay"/> a glint sweeps across the face-down card.</summary>
        public IEnumerator PlayShimmer(float delay)
        {
            for (float t = 0; t < delay; t += _delta()) yield return null;
            if (IsUp || IsMatched || IsGone) yield break;
            var rt = _shine.rectTransform;
            float w = Rect.rect.width;
            rt.sizeDelta = new Vector2(w * 0.28f, Rect.rect.height * 1.8f);
            _shine.enabled = true;
            yield return Animate(ShimmerSeconds, _delta, t =>
            {
                float e = t * t * (3f - 2f * t); // smoothstep
                rt.anchorMin = rt.anchorMax = new Vector2(Mathf.Lerp(-0.4f, 1.4f, e), 0.5f);
                rt.anchoredPosition = Vector2.zero;
                var c = _shine.color;
                c.a = 0.38f * Mathf.Sin(t * Mathf.PI); // fade in and out at the edges
                _shine.color = c;
                _shine.enabled = !IsUp; // hide at once if the card gets turned over mid-sweep
            });
            _shine.enabled = false;
        }

        public const float ShimmerSeconds = 0.7f;

        void Refresh() => _button.interactable = !IsUp && !IsMatched;
    }
}
