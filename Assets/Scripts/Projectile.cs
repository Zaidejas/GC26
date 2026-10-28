using UnityEngine;

// Пуля игрока (летит вверх) или ракета пришельца (летит вниз)
public class Projectile : MonoBehaviour
{
    public Vector2 direction = Vector2.up;
    public float speed = 12f;
    public bool fromPlayer = true;
    public bool damagesBunkers = false;
    public float destroyBeyondY = 8f;

    bool used;

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        if (Mathf.Abs(transform.position.y) > destroyBeyondY)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Защита от двойного попадания в одном кадре
        if (used) return;

        if (fromPlayer && other.TryGetComponent(out Invader invader))
        {
            invader.Kill();
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
            Consume();
        }
    }

    void Consume()
    {
        used = true;
        Destroy(gameObject);
    }
}
