using UnityEngine;

// Вспышка взрыва: растёт, исчезает и удаляется
public class Explosion : MonoBehaviour
{
    public float lifetime = 0.35f;
    public float growth = 0.6f;

    SpriteRenderer spriteRenderer;
    Vector3 startScale;
    float timer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        transform.localScale = startScale * (1f + t * growth);
        Color color = spriteRenderer.color;
        color.a = 1f - t;
        spriteRenderer.color = color;

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}
