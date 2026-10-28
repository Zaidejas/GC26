using System.Collections;
using UnityEngine;

// Корабль игрока: движение влево/вправо и автоматическая стрельба
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    public float speed = 7f;
    public float edgePadding = 0.6f;

    [Header("Стрельба")]
    public Projectile laserPrefab;
    public float fireCooldown = 0.45f;

    [Header("Урон")]
    public float invulnerableTime = 1.5f;

    SpriteRenderer spriteRenderer;
    float minX, maxX;
    float fireTimer;
    bool invulnerable;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Границы считаем от камеры, чтобы работало при любом разрешении
        Camera cam = Camera.main;
        minX = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x + edgePadding;
        maxX = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x - edgePadding;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        Move();
        AutoFire();
    }

    void Move()
    {
        Vector3 pos = transform.position;
        pos.x += GameInput.Horizontal() * speed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;
    }

    void AutoFire()
    {
        // На паузе Time.deltaTime = 0, поэтому таймер стоит и стрельба тоже
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;

        Instantiate(laserPrefab, transform.position + Vector3.up * 0.6f, Quaternion.identity);
        SoundManager.Play(Sfx.Shoot);
        fireTimer = fireCooldown;
    }

    // Вызывается ракетой пришельца
    public void Hit()
    {
        if (invulnerable || GameManager.Instance.IsGameOver) return;

        SoundManager.Play(Sfx.PlayerHit);
        GameManager.Instance.LoseLife();

        if (!GameManager.Instance.IsGameOver)
            StartCoroutine(BlinkRoutine());
    }

    // Мигание и неуязвимость после попадания
    IEnumerator BlinkRoutine()
    {
        invulnerable = true;
        float elapsed = 0f;
        while (elapsed < invulnerableTime)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        spriteRenderer.enabled = true;
        invulnerable = false;
    }
}
