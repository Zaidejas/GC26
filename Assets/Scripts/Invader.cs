using UnityEngine;

// Один пришелец: анимация, очки, выпадение монетки
public class Invader : MonoBehaviour
{
    public int points = 10;

    [Header("Анимация")]
    public Sprite[] frames;
    public float frameTime = 0.5f;

    [Header("Эффекты")]
    public GameObject explosionPrefab;
    public Coin coinPrefab;

    [Header("Бункеры")]
    [Tooltip("Ниже этой высоты пришелец ломает бункеры, через которые проходит")]
    public float bunkerCheckY = -1.9f;

    public int Row { get; private set; }
    public int Column { get; private set; }

    InvaderGrid grid;
    float coinDropChance;
    SpriteRenderer spriteRenderer;
    BoxCollider2D box;
    int frame;
    float frameTimer;
    bool dead;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
    }

    public void Init(InvaderGrid owner, int row, int column, float dropChance)
    {
        grid = owner;
        Row = row;
        Column = column;
        coinDropChance = dropChance;
    }

    void Update()
    {
        Animate();

        if (transform.position.y < bunkerCheckY)
            BreakBunkers();
    }

    void Animate()
    {
        if (frames == null || frames.Length < 2) return;

        frameTimer += Time.deltaTime;
        if (frameTimer < frameTime) return;

        frameTimer = 0f;
        frame = (frame + 1) % frames.Length;
        spriteRenderer.sprite = frames[frame];
    }

    void BreakBunkers()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent(out BunkerBlock block))
                block.Break();
        }
    }

    // Вызывается пулей игрока
    public void Kill()
    {
        if (dead) return;
        dead = true;

        GameManager.Instance.AddScore(points);
        SoundManager.Play(Sfx.Explosion);

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        if (coinPrefab != null && Random.value < coinDropChance)
            Instantiate(coinPrefab, transform.position, Quaternion.identity);

        if (grid != null)
            grid.OnInvaderKilled(this);

        Destroy(gameObject);
    }
}
