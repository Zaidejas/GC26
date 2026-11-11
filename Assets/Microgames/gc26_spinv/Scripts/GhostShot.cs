using UnityEngine;

namespace GC26.Spinv
{
    // Призрачная пуля Phantom-а в ярости: почти прозрачная, проявляется только ближе к игроку
    [RequireComponent(typeof(SpriteRenderer))]
    public class GhostShot : MonoBehaviour
    {
        [Tooltip("Ниже этой высоты пуля видна полностью")]
        public float revealY;
        [Tooltip("За сколько юнитов до revealY начинает проявляться")]
        public float fadeDistance = 2f;
        [Range(0f, 1f)] public float hiddenAlpha = 0.12f;

        SpriteRenderer sprite;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            float k = Mathf.Clamp01((revealY + fadeDistance - transform.position.y) / fadeDistance);
            Color c = sprite.color;
            c.a = Mathf.Lerp(hiddenAlpha, 1f, k);
            sprite.color = c;
        }
    }
}
