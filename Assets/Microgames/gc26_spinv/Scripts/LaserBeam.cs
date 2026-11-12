using UnityEngine;

namespace GC26.Spinv
{
    // Лазер босса: сначала тонкая линия-предупреждение, потом широкий луч на всю высоту
    [RequireComponent(typeof(SpriteRenderer))]
    public class LaserBeam : MonoBehaviour
    {
        public float warnTime = 1f;
        public float fireTime = 0.5f;
        public float warnWidth = 0.15f;
        public float fireWidth = 1f;
        public Color warnColor = new Color(0.66f, 1f, 0.74f, 0.45f);
        public Color fireColor = new Color(0.9f, 1f, 0.93f, 0.95f);
        [Tooltip("Половина ширины корабля: луч задевает его краем")]
        public float playerHalfWidth = 0.9f;

        public float TotalTime => warnTime + fireTime;

        PlayerController player;
        SpriteRenderer spriteRenderer;
        float height;
        float timer;
        bool firing;

        public void Init(PlayerController target, float x, float topY, float bottomY)
        {
            player = target;
            spriteRenderer = GetComponent<SpriteRenderer>();
            height = topY - bottomY;
            transform.position = new Vector3(x, (topY + bottomY) / 2f, 0f);
            SetWidth(warnWidth);
            spriteRenderer.color = warnColor;
        }

        void SetWidth(float width)
        {
            Vector2 size = spriteRenderer.sprite.bounds.size;
            transform.localScale = new Vector3(width / size.x, height / size.y, 1f);
        }

        void Update()
        {
            timer += Time.deltaTime;

            if (!firing && timer >= warnTime)
                Fire();

            if (firing && player != null && Mathf.Abs(player.transform.position.x - transform.position.x) < fireWidth / 2f + playerHalfWidth)
                player.Hit();

            if (timer >= TotalTime)
                Destroy(gameObject);
        }

        void Fire()
        {
            firing = true;
            SetWidth(fireWidth);
            spriteRenderer.color = fireColor;
            SpinvSoundManager.Play(Sfx.Laser);

            Vector2 boxSize = new Vector2(fireWidth, height);
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(transform.position, boxSize, 0f))
            {
                if (hit.TryGetComponent(out BunkerBlock block))
                    block.Break();
            }
        }
    }
}
