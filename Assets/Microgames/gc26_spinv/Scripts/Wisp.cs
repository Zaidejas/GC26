using UnityEngine;

namespace GC26.Spinv
{
    // Огонёк Phantom-а: вылетает из босса и медленно преследует корабль, плавно поворачивая.
    // Сбивается одним попаданием. Задел корабль - ранит и гаснет. Через lifetime гаснет сам
    [RequireComponent(typeof(SpriteRenderer))]
    public class Wisp : MonoBehaviour
    {
        public float speed = 3.2f;
        [Tooltip("Насколько быстро поворачивает к кораблю, градусов в секунду")]
        public float turnRate = 110f;
        public float lifetime = 5f;
        public float hitDistance = 0.55f;
        public float pulseSpeed = 8f;
        public GameObject explosionPrefab;

        PlayerController player;
        Vector2 heading = Vector2.down;
        float age;
        Vector3 baseScale;
        SpriteRenderer sprite;

        public void Init(PlayerController target, Vector2 startHeading)
        {
            player = target;
            heading = startHeading.normalized;
        }

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (player != null)
            {
                Vector2 toPlayer = (Vector2)(player.transform.position - transform.position);
                float angle = Vector2.SignedAngle(heading, toPlayer);
                float turn = Mathf.Clamp(angle, -turnRate * Time.deltaTime, turnRate * Time.deltaTime);
                heading = Quaternion.Euler(0f, 0f, turn) * heading;

                if (toPlayer.magnitude < hitDistance)
                {
                    player.Hit();
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position += (Vector3)(heading * speed * Time.deltaTime);

            // Пульсирует, в последнюю секунду гаснет
            float pulse = 1f + 0.12f * Mathf.Sin(age * pulseSpeed);
            transform.localScale = baseScale * pulse;
            Color c = sprite.color;
            c.a = Mathf.Clamp01(lifetime - age);
            sprite.color = c;
        }

        // Вызывается пулей игрока
        public void Hit()
        {
            if (explosionPrefab != null)
            {
                GameObject boom = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                boom.transform.localScale = Vector3.one * 1.2f;
            }
            Destroy(gameObject);
        }
    }
}
