using System.Collections;
using UnityEngine;

public enum BossAttack { Fan, Burst, Dash, Wall, Spiral, Laser, Summon }

// Босс: двигается вбок и чередует атаки. При половине здоровья может войти в ярость
public class Boss : MonoBehaviour
{
    [Header("Здоровье и награда")]
    public int maxHealth = 100;
    public int points = 500;
    public Coin coinPrefab;
    [Tooltip("Монетка выпадает каждые N попаданий")]
    public int coinEveryHits = 8;
    public GameObject explosionPrefab;

    [Header("Движение")]
    public float moveSpeed = 3f;
    public float edgePadding = 0.3f;
    public float dashSpeed = 20f;

    [Header("Атаки")]
    public BossAttack[] attacks = { BossAttack.Fan };
    public float firstAttackDelay = 1f;
    [Tooltip("Пауза между атаками")]
    public float attackPause = 1f;
    public Projectile missilePrefab;
    public float missileSpeed = 9f;

    [Header("Веер")]
    public int fanCount = 5;
    public float fanAngle = 30f;
    public int fanVolleys = 3;
    public float fanInterval = 0.8f;

    [Header("Очередь")]
    public int burstCount = 6;
    public float burstInterval = 0.15f;

    [Header("Рывок")]
    public int dashVolleyCount = 5;
    public float dashVolleyAngle = 12f;

    [Header("Стена с дырой")]
    public float wallSpacing = 0.9f;
    [Tooltip("Запас прохода с каждой стороны корабля, юнитов (включая полуширину ракеты)")]
    public float wallGapMargin = 0.75f;
    public int wallCount = 2;
    public float wallInterval = 1.2f;

    [Header("Спираль")]
    public float spiralDuration = 3f;
    public float spiralInterval = 0.12f;
    public float spiralStep = 22f;
    public float spiralMissileSpeed = 7f;

    [Header("Лазер")]
    public LaserBeam laserPrefab;
    public float laserBottomY = -14f;

    [Header("Призыв")]
    public Invader minionPrefab;
    public int minionsPerSummon = 2;
    public float minionFallSpeed = 2.5f;

    [Tooltip("Финальный босс: игрок «устаёт» и время от времени моргает")]
    public bool playerBlinks = false;

    [Header("Эффектная смерть (финальный босс)")]
    [Tooltip("Вместо одного взрыва - анимация ~5 с: цепочка взрывов, нарастающая тряска, большой взрыв")]
    public bool dramaticDeath = false;
    public float deathChainTime = 3.2f;
    public float deathAfterTime = 1.8f;

    [Header("Ярость при половине здоровья")]
    public bool rageAtHalfHealth = false;
    [Tooltip("Во сколько раз чаще идут атаки в ярости")]
    public float rageFireRate = 1.5f;
    [Range(0f, 1f)]
    [Tooltip("Шанс запустить сразу две атаки")]
    public float doubleAttackChance = 0.35f;
    public Color rageColor = new Color(0.55f, 0.75f, 0.55f);

    [Header("Эффект попадания")]
    public Color hitColor = new Color(0.3f, 0.45f, 0.3f);
    public float flashTime = 0.1f;

    public event System.Action Died;

    public float HealthFraction => (float)health / maxHealth;
    // Убит: идёт анимация смерти или босс уже уничтожен
    public bool IsDead => dead;
    bool Raging => rageAtHalfHealth && health * 2 <= maxHealth;
    Vector3 Muzzle => transform.position + Vector3.down * spriteRenderer.bounds.extents.y;

    PlayerController player;
    [Tooltip("Картинка босса. Лежит в дочернем Visual, чтобы тряска не двигала коллайдер")]
    [SerializeField] SpriteRenderer spriteRenderer;
    int health;
    int hits;
    float minX, maxX;
    float leftEdge, rightEdge;
    float direction = 1f;
    float flashTimer;
    int movementHolds;
    int lastAttack = -1;
    bool dead;

    public void Init(PlayerController target)
    {
        player = target;
    }

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = maxHealth;
    }

    void Start()
    {
        Camera cam = Camera.main;
        leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x;
        rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x;
        float halfWidth = spriteRenderer.bounds.extents.x + edgePadding;
        minX = leftEdge + halfWidth;
        maxX = rightEdge - halfWidth;

        StartCoroutine(AttackLoop());
    }

    void Update()
    {
        if (dead) return; // во время анимации смерти босс стоит, цветом управляет BossAnger
        if (movementHolds == 0)
            Move();
        UpdateColor();
    }

    void Move()
    {
        Vector3 pos = transform.position;
        pos.x += direction * moveSpeed * Time.deltaTime;

        if (pos.x > maxX) direction = -1f;
        else if (pos.x < minX) direction = 1f;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;
    }

    void UpdateColor()
    {
        flashTimer -= Time.deltaTime;
        if (flashTimer > 0f)
            spriteRenderer.color = hitColor;
        else
            spriteRenderer.color = Raging ? rageColor : Color.white;
    }

    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        while (true)
        {
            BossAttack attack = NextAttack();

            if (Raging && attacks.Length > 1 && Random.value < doubleAttackChance)
                StartCoroutine(TrackAttack(NextAttack()));

            yield return StartCoroutine(TrackAttack(attack));

            // Не больше двух атак одновременно: следующая начнётся, когда закончатся обе
            while (runningAttacks > 0)
                yield return null;

            yield return new WaitForSeconds(Raging ? attackPause / rageFireRate : attackPause);
        }
    }

    int runningAttacks;

    IEnumerator TrackAttack(BossAttack attack)
    {
        runningAttacks++;
        yield return StartCoroutine(RunAttack(attack));
        runningAttacks--;
    }

    // Случайная атака, но не та же, что была только что
    BossAttack NextAttack()
    {
        int index = Random.Range(0, attacks.Length);
        if (attacks.Length > 1 && index == lastAttack)
            index = (index + 1) % attacks.Length;
        lastAttack = index;
        return attacks[index];
    }

    IEnumerator RunAttack(BossAttack attack)
    {
        switch (attack)
        {
            case BossAttack.Fan: return FanAttack();
            case BossAttack.Burst: return BurstAttack();
            case BossAttack.Dash: return DashAttack();
            case BossAttack.Wall: return WallAttack();
            case BossAttack.Spiral: return SpiralAttack();
            case BossAttack.Laser: return LaserAttack();
            default: return SummonAttack();
        }
    }

    float Wait(float seconds) => Raging ? seconds / rageFireRate : seconds;

    void SpawnMissile(Vector3 position, Vector2 shotDirection, float speed)
    {
        Projectile missile = Instantiate(missilePrefab, position, Quaternion.identity);
        missile.speed = speed;
        missile.SetDirection(shotDirection);
    }

    void FireFan(int count, float angle)
    {
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;
            Vector2 shot = Quaternion.Euler(0f, 0f, Mathf.Lerp(-angle, angle, t)) * Vector3.down;
            SpawnMissile(Muzzle, shot, missileSpeed);
        }
    }

    IEnumerator FanAttack()
    {
        for (int i = 0; i < fanVolleys; i++)
        {
            FireFan(fanCount, fanAngle);
            yield return new WaitForSeconds(Wait(fanInterval));
        }
    }

    IEnumerator BurstAttack()
    {
        for (int i = 0; i < burstCount; i++)
        {
            if (player != null)
                SpawnMissile(Muzzle, player.transform.position - Muzzle, missileSpeed);
            yield return new WaitForSeconds(Wait(burstInterval));
        }
    }

    IEnumerator MoveAbovePlayer()
    {
        if (player == null) yield break;

        float targetX = Mathf.Clamp(player.transform.position.x, minX, maxX);
        while (!Mathf.Approximately(transform.position.x, targetX))
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, targetX, dashSpeed * Time.deltaTime);
            transform.position = pos;
            yield return null;
        }
    }

    IEnumerator DashAttack()
    {
        movementHolds++;
        yield return MoveAbovePlayer();
        FireFan(dashVolleyCount, dashVolleyAngle);
        yield return new WaitForSeconds(0.2f);
        movementHolds--;
    }

    IEnumerator WallAttack()
    {
        for (int wall = 0; wall < wallCount; wall++)
        {
            // Проход ставим туда, куда корабль реально может встать, и шириной от самого корабля.
            // Раньше дыра считалась в клетках от края экрана и у краёв была уже корабля
            float gapCenter = Random.Range(player.MinX, player.MaxX);
            float gapHalf = player.HalfWidth + wallGapMargin;

            int cells = Mathf.CeilToInt((rightEdge - leftEdge) / wallSpacing) + 1;
            for (int i = 0; i < cells; i++)
            {
                float x = leftEdge + i * wallSpacing;
                if (Mathf.Abs(x - gapCenter) < gapHalf) continue;
                SpawnMissile(new Vector3(x, Muzzle.y, 0f), Vector2.down, missileSpeed);
            }
            yield return new WaitForSeconds(Wait(wallInterval));
        }
    }

    IEnumerator SpiralAttack()
    {
        float angle = Random.Range(0f, 360f);
        for (float elapsed = 0f; elapsed < spiralDuration; elapsed += spiralInterval)
        {
            SpawnMissile(Muzzle, Quaternion.Euler(0f, 0f, angle) * Vector3.down, spiralMissileSpeed);
            SpawnMissile(Muzzle, Quaternion.Euler(0f, 0f, angle + 180f) * Vector3.down, spiralMissileSpeed);
            angle += spiralStep;
            yield return new WaitForSeconds(spiralInterval);
        }
    }

    // Босс встаёт над игроком и замирает, пока идёт предупреждение и работает луч
    IEnumerator LaserAttack()
    {
        movementHolds++;
        yield return MoveAbovePlayer();

        LaserBeam beam = Instantiate(laserPrefab);
        beam.Init(player, transform.position.x, Muzzle.y, laserBottomY);
        yield return new WaitForSeconds(beam.TotalTime);

        movementHolds--;
    }

    IEnumerator SummonAttack()
    {
        for (int i = 0; i < minionsPerSummon; i++)
        {
            float x = Random.Range(minX, maxX);
            Invader minion = Instantiate(minionPrefab, new Vector3(x, transform.position.y, 0f), Quaternion.identity);
            minion.Init(null, 0, 0, 0f);

            FallingMinion falling = minion.gameObject.AddComponent<FallingMinion>();
            falling.fallSpeed = minionFallSpeed;
            falling.player = player;
        }
        yield return new WaitForSeconds(Wait(1f));
    }

    // Вызывается пулей игрока
    public void Hit()
    {
        if (dead) return;

        health--;
        hits++;
        flashTimer = flashTime;

        if (coinPrefab != null && hits % coinEveryHits == 0)
            Instantiate(coinPrefab, transform.position, Quaternion.identity);

        if (health <= 0)
            Die();
    }

    void Die()
    {
        dead = true;
        StopAllCoroutines();
        GameManager.Instance.AddScore(points);

        foreach (FallingMinion minion in Object.FindObjectsByType<FallingMinion>())
            Destroy(minion.gameObject);
        foreach (LaserBeam beam in Object.FindObjectsByType<LaserBeam>())
            Destroy(beam.gameObject);

        if (dramaticDeath)
        {
            StartCoroutine(DramaticDeath());
            return;
        }

        SoundManager.Play(Sfx.Explosion);
        Explode(transform.position, 4f);
        Died?.Invoke();
        Destroy(gameObject);
    }

    void Explode(Vector3 position, float scale)
    {
        if (explosionPrefab == null) return;
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.transform.localScale = Vector3.one * scale;
    }

    // Финальный босс: замирает и краснеет, пули исчезают, по телу всё чаще рвутся мелкие взрывы
    // и нарастает тряска, затем большой взрыв с тряской экрана и короткая пауза перед победой.
    // Игрок в это время не стреляет
    IEnumerator DramaticDeath()
    {
        if (player != null) player.canShoot = false;
        var hitbox = GetComponent<Collider2D>();
        if (hitbox != null) hitbox.enabled = false;
        foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
            Destroy(projectile.gameObject);

        var anger = GetComponent<BossAnger>();
        spriteRenderer.color = Color.white; // убрать затемнение от последнего попадания
        Bounds bounds = spriteRenderer.bounds;
        int sounds = 0;

        for (float t = 0f; t < deathChainTime; )
        {
            float k = t / deathChainTime;
            if (anger != null) anger.dyingShake = Mathf.Lerp(0.6f, 2f, k);

            Vector3 point = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x) * 0.85f + bounds.center.x * 0.15f,
                Random.Range(bounds.min.y, bounds.max.y) * 0.85f + bounds.center.y * 0.15f, 0f);
            Explode(point, Random.Range(1.2f, 2.2f));
            if (sounds++ % 3 == 0) SoundManager.Play(Sfx.Explosion);

            // Взрывы всё чаще: от 0.28 до 0.07 с
            float wait = Mathf.Lerp(0.28f, 0.07f, k);
            yield return new WaitForSeconds(wait);
            t += wait;
        }

        // Большой взрыв: в центре и вспышки вокруг, босс исчезает, трясётся весь экран
        SoundManager.Play(Sfx.Explosion);
        Explode(bounds.center, 9f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Explode(bounds.center + new Vector3(Mathf.Cos(a) * bounds.extents.x * 0.8f, Mathf.Sin(a) * bounds.extents.y, 0f), 4f);
        }
        if (anger != null) anger.dyingShake = -1f;
        spriteRenderer.gameObject.SetActive(false);
        yield return ShakeCamera(0.5f, 0.35f);

        yield return new WaitForSeconds(Mathf.Max(0f, deathAfterTime - 0.5f));
        Died?.Invoke();
        Destroy(gameObject);
    }

    static IEnumerator ShakeCamera(float duration, float strength)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 home = cam.transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float s = strength * (1f - t / duration);
            cam.transform.position = home + new Vector3(Random.Range(-s, s), Random.Range(-s, s), 0f);
            yield return null;
        }
        cam.transform.position = home;
    }
}
