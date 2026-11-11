using UnityEngine;

namespace GC26.Spinv
{
    // Растягивает фон на весь экран камеры при любом соотношении сторон, без искажения пропорций
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScreenBackground : MonoBehaviour
    {
        [Tooltip("Небольшой запас, чтобы края картинки не попадали в кадр")]
        public float overscan = 1.01f;

        SpriteRenderer spriteRenderer;
        float lastAspect;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || Mathf.Approximately(cam.aspect, lastAspect)) return;

            lastAspect = cam.aspect;
            Fit(cam);
        }

        void Fit(Camera cam)
        {
            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;
            Vector2 size = spriteRenderer.sprite.bounds.size;

            float scale = Mathf.Max(width / size.x, height / size.y) * overscan;
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);
        }
    }
}
