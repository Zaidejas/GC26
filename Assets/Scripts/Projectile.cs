using UnityEngine;

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
