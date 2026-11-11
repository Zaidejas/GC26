using UnityEngine;

namespace GC26.Spinv
{
    // Пуля игрока (летит вверх) или ракета пришельца (летит вниз)
    public class Projectile : MonoBehaviour
    {
        public Vector2 direction = Vector2.up;
        public float speed = 12f;
        public bool fromPlayer = true;
        public bool damagesBunkers = false;
        [Tooltip("Радиус поломки бункера вокруг попадания. 0 - только один блок")]
        public float bunkerDamageRadius = 0f;
        public float destroyBeyondY = 15f;
        public float destroyBeyondX = 12f;
        [Tooltip("Вращение спрайта в полёте, градусов в секунду (ромб пришельцев)")]
        public float spinSpeed = 0f;
        [Tooltip("Ускорение вниз, юнитов/с². 0 - летит прямо с постоянной скоростью (бомбы - по дуге)")]
        public float gravity = 0f;

        bool used;
        Vector2 velocity;
        bool falling;

        // Бросок по дуге: начальная скорость с боковой составляющей, дальше тянет вниз gravity
        public void Throw(Vector2 startVelocity)
        {
            velocity = startVelocity;
            falling = true;
        }

        // Задаёт направление полёта и поворачивает ракету носом вперёд
        public void SetDirection(Vector2 newDirection)
        {
            direction = newDirection.normalized;
            transform.rotation = Quaternion.FromToRotation(Vector3.down, direction);
        }

        // Вращающаяся пуля нарисована квадратом: с первого кадра ставим её ромбом
        void Start()
        {
            if (spinSpeed != 0f)
                transform.Rotate(0f, 0f, 45f);
        }

        void Update()
        {
            if (falling)
            {
                velocity += Vector2.down * gravity * Time.deltaTime;
                transform.Translate(velocity * Time.deltaTime, Space.World);
            }
            else
            {
                transform.Translate(direction * speed * Time.deltaTime, Space.World);
            }
            if (spinSpeed != 0f)
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // Боковые ракеты спирали иначе летели бы за экраном вечно
            if (Mathf.Abs(transform.position.y) > destroyBeyondY || Mathf.Abs(transform.position.x) > destroyBeyondX)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Защита от двойного попадания в одном кадре
            if (used) return;

            if (fromPlayer && other.TryGetComponent(out Invader invader))
            {
                invader.Hit();
                Consume();
            }
            else if (fromPlayer && other.TryGetComponent(out Boss boss))
            {
                boss.Hit();
                Consume();
            }
            else if (fromPlayer && other.TryGetComponent(out PhantomDecoy decoy))
            {
                decoy.Hit();
                Consume();
            }
            else if (fromPlayer && other.TryGetComponent(out Wisp wisp))
            {
                wisp.Hit();
                Consume();
            }
            else if (!fromPlayer && other.TryGetComponent(out PlayerController player))
            {
                player.Hit();
                Consume();
            }
            else if (damagesBunkers && other.TryGetComponent(out BunkerBlock block))
            {
                block.Break();
                BreakBlocksAround(block.transform.position);
                Consume();
            }
        }

        void BreakBlocksAround(Vector2 center)
        {
            if (bunkerDamageRadius <= 0f) return;

            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, bunkerDamageRadius))
            {
                if (hit.TryGetComponent(out BunkerBlock block))
                    block.Break();
            }
        }

        void Consume()
        {
            used = true;
            Destroy(gameObject);
        }
    }
}
