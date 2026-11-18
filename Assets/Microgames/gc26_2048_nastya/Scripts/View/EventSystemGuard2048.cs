using UnityEngine;
using UnityEngine.EventSystems;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Keeps exactly one EventSystem (TDD 7.3): if the shell already has an active one,
    /// the game scene's own EventSystem object is switched off. Put it on that object.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(EventSystem))]
    public sealed class EventSystemGuard2048 : MonoBehaviour
    {
        void Awake()
        {
            var own = GetComponent<EventSystem>();
            foreach (var other in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude))
            {
                if (other != own && other.isActiveAndEnabled)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
        }
    }
}
