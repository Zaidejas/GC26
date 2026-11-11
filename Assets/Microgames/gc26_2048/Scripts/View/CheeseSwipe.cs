using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GC26._2048
{
    /// <summary>Turns a swipe anywhere over the field area into a direction.</summary>
    public class CheeseSwipe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action<Direction> Swiped;

        Vector2 start;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            start = e.position;
            down = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!down) return;
            down = false;
            var d = e.position - start;
            float threshold = Mathf.Max(30f, Mathf.Min(Screen.width, Screen.height) * 0.05f);
            if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) < threshold) return;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                Swiped?.Invoke(d.x > 0 ? Direction.Right : Direction.Left);
            else
                Swiped?.Invoke(d.y > 0 ? Direction.Up : Direction.Down);
        }
    }
}
