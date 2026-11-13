using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// An invisible area that reports horizontal swipes (finger or mouse): while dragging, how far the finger
    /// has moved sideways; when it lets go, that distance and how long the swipe took. A mostly vertical move
    /// isn't a swipe. Buttons on top of it keep working as usual.
    /// </summary>
    public sealed class SwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>The finger moved: sideways distance in pixels (positive: to the right).</summary>
        public event Action<float> Moved;
        /// <summary>The finger let go: sideways distance in pixels and the swipe's length in seconds.</summary>
        public event Action<float, float> Released;

        bool swiping;
        float startTime;

        public static SwipeArea Create(RectTransform parent)
        {
            var rt = (RectTransform)new GameObject("Swipe area", typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling(); // behind everything, so buttons still get their taps
            var image = rt.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            return rt.gameObject.AddComponent<SwipeArea>();
        }

        /// <summary>The picture that catches the finger (it must stay a raycast target).</summary>
        public Graphic Target => GetComponent<Image>();

        public void OnBeginDrag(PointerEventData e)
        {
            var d = e.position - e.pressPosition;
            swiping = Mathf.Abs(d.x) >= Mathf.Abs(d.y);
            startTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData e)
        {
            if (swiping) Moved?.Invoke(e.position.x - e.pressPosition.x);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!swiping) return;
            swiping = false;
            Released?.Invoke(e.position.x - e.pressPosition.x, Time.unscaledTime - startTime);
        }
    }
}
