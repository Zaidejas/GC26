using UnityEngine;

namespace GC26.Spinv
{
    // Мигающий спрайт (ядро бомбы бомбардира): включается и гаснет через равные промежутки
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteBlink : MonoBehaviour
    {
        public float interval = 0.18f;
        [Range(0f, 1f)] public float dimAlpha = 0.25f;

        SpriteRenderer sprite;
        float timer;
        bool bright = true;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            timer = Random.Range(0f, interval); // соседние бомбы мигают не в такт
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            bright = !bright;
            Color c = sprite.color;
            c.a = bright ? 1f : dimAlpha;
            sprite.color = c;
        }
    }
}
