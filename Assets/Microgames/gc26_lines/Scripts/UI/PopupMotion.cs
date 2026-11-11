using GC26.Lines.View;
using UnityEngine;

namespace GC26.Lines.UI
{
    /// <summary>
    /// How pop-ups come and go: the dim background fades in while the card grows from 90 % with a
    /// slight overshoot; closing fades everything out a little faster. The pop-up counts as closed
    /// (and stops catching taps) the moment closing starts.
    /// </summary>
    public sealed class PopupMotion
    {
        public const float OpenSeconds = 0.22f, CloseSeconds = 0.14f, StartScale = 0.9f, EndScale = 0.96f;

        readonly GameObject root;
        readonly CanvasGroup group;
        readonly RectTransform card;

        public bool IsOpen { get; private set; }

        public PopupMotion(GameObject root, RectTransform card)
        {
            this.root = root;
            this.card = card;
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
        }

        public void Open()
        {
            IsOpen = true;
            Tween.Kill(group);
            Tween.Kill(card);
            root.SetActive(true);
            group.blocksRaycasts = true;
            group.alpha = 0f;
            card.localScale = Vector3.one * StartScale;
            Tween.To(group, OpenSeconds, p => group.alpha = p, Ease.OutQuad);
            Tween.To(card, OpenSeconds, p => card.localScale = Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, p), Ease.OutBack);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Tween.Kill(group);
            Tween.Kill(card);
            group.blocksRaycasts = false; // taps go through straight away
            float alpha = group.alpha, scale = card.localScale.x;
            Tween.To(card, CloseSeconds, p => card.localScale = Vector3.one * Mathf.Lerp(scale, EndScale, p), Ease.InQuad);
            Tween.To(group, CloseSeconds, p => group.alpha = alpha * (1f - p), Ease.InQuad)
                .OnComplete(() => { if (!IsOpen && root != null) root.SetActive(false); });
        }
    }
}
