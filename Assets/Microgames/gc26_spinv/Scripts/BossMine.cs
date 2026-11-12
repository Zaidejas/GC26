using UnityEngine;

namespace GC26.Spinv
{
    // Мина Bomber-а: летит от босса по дуге (как брошенная) в случайное место на высоте корабля, ложится,
    // красный огонёк мигает всё быстрее (вокруг - кольцо радиуса взрыва), и мина взрывается.
    // Задевает корабль, если он внутри кольца
    public class BossMine : MonoBehaviour
    {
        public SpriteRenderer body;
        [Tooltip("Красный огонёк в середине: мигает перед взрывом")]
        public SpriteRenderer lamp;
        public SpriteRenderer ring;
        public GameObject explosionPrefab;
        [Tooltip("Ускорение падения, юнитов/с²: мина летит по дуге")]
        public float gravity = 10f;
        public float radius = 1.5f;
        [Tooltip("Мина, задевшая в полёте бункер, ложится на него. Радиус, которым она его задевает")]
        public float bunkerTouchRadius = 0.15f;
        [Tooltip("Сколько бункера выбивает взрыв мины, лежащей на нём")]
        public float bunkerBreakRadius = 0.6f;
        public float spinSpeed = 120f;
        public Color ringColor = new Color(1f, 0.34f, 0.29f, 0.55f);

        PlayerController player;
        Vector3 landPoint;
        Vector2 velocity;
        float flightTime, flightLeft;
        float fuse, fuseLeft;
        bool landed;
        bool onBunker;

        // Бросок в точку landing: без начальной скорости по вертикали, вбок - ровно столько,
        // чтобы долететь до точки за время свободного падения
        public void Init(PlayerController target, Vector2 landing, float fuseTime)
        {
            player = target;
            landPoint = new Vector3(landing.x, landing.y, transform.position.z);
            fuse = fuseLeft = fuseTime;

            float drop = Mathf.Max(0.1f, transform.position.y - landing.y);
            flightTime = flightLeft = Mathf.Sqrt(2f * drop / Mathf.Max(0.1f, gravity));
            velocity = new Vector2((landing.x - transform.position.x) / flightTime, 0f);
        }

        void Start()
        {
            if (ring == null) return;
            // Кольцо ровно в радиус взрыва, как бы ни был отмасштабирован объект
            float spriteSize = ring.sprite != null ? ring.sprite.bounds.size.x : 1f;
            float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            ring.transform.localScale = Vector3.one * (radius * 2f / spriteSize / scale);
            ring.enabled = false;
        }

        void Update()
        {
            if (!landed)
            {
                velocity += Vector2.down * gravity * Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                transform.Rotate(0f, 0f, spinSpeed * Mathf.Sign(-velocity.x + 0.001f) * Time.deltaTime);
                flightLeft -= Time.deltaTime;
                if (flightLeft <= 0f || transform.position.y <= landPoint.y)
                {
                    transform.position = landPoint;
                    landed = true;
                }
                else if (velocity.y < 0f && TouchesBunker())
                {
                    // Бункер укрывает: мина ложится на него и взрывается там
                    landed = true;
                    onBunker = true;
                }
                return;
            }

            fuseLeft -= Time.deltaTime;
            if (fuseLeft <= 0f)
            {
                Explode();
                return;
            }

            // Огонёк мигает всё чаще: от 4 до 16 раз в секунду
            float k = 1f - fuseLeft / Mathf.Max(0.01f, fuse);
            float rate = Mathf.Lerp(4f, 16f, k);
            bool on = Mathf.Repeat(Time.time * rate, 1f) < 0.5f;
            if (lamp != null) lamp.enabled = on;
            if (ring != null)
            {
                ring.enabled = true;
                Color c = ringColor;
                c.a = ringColor.a * (on ? 1f : 0.5f) * Mathf.Lerp(0.5f, 1f, k);
                ring.color = c;
            }
        }

        void Explode()
        {
            SpinvSoundManager.Play(Sfx.BombExplosion);
            if (explosionPrefab != null)
            {
                GameObject boom = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                boom.transform.localScale = Vector3.one * radius * 1.6f;
            }
            if (onBunker)
            {
                foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, bunkerBreakRadius))
                    if (hit.TryGetComponent(out BunkerBlock block)) block.Break();
            }
            if (player != null && Vector2.Distance(player.transform.position, transform.position) < radius)
                player.Hit();
            Destroy(gameObject);
        }

        bool TouchesBunker()
        {
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, bunkerTouchRadius))
                if (hit.GetComponent<BunkerBlock>() != null) return true;
            return false;
        }
    }
}
