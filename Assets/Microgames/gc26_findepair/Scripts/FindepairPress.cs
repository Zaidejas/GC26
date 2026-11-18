using UnityEngine;
using UnityEngine.EventSystems;

namespace GC26.Findepair
{
    /// <summary>Buttons shrink a little while pressed.</summary>
    public class FindepairPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e) => transform.localScale = Vector3.one * 0.96f;
        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
        void OnDisable() => transform.localScale = Vector3.one;
    }
}
