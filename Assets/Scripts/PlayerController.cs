using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Корабль игрока: идёт за пальцем по горизонтали и автоматически стреляет
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Скорость движения с клавиатуры")]
    public float speed = 7f;
    [Tooltip("С какой скоростью корабль догоняет палец")]
    public float followSpeed = 40f;
    public float edgePadding = 1.2f;

    [Header("Стрельба")]
    public Projectile laserPrefab;
    public float fireCooldown = 0.45f;

    [Header("Урон")]
    public float invulnerableTime = 1.5f;

    SpriteRenderer spriteRenderer;
    Camera cam;
    float minX, maxX;
    float fireTimer;
    bool invulnerable;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Границы считаем от камеры, чтобы работало при любом разрешении
        cam = Camera.main;
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

        if (GameInput.PointerHeld(out Vector2 screenPosition) && !IsOverUI())
        {
            float targetX = cam.ScreenToWorldPoint(screenPosition).x;
            pos.x = Mathf.MoveTowards(pos.x, targetX, followSpeed * Time.deltaTime);
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;
    }

    // Нажатие на кнопку интерфейса не должно двигать корабль
    static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void AutoFire()
    {
        // На паузе Time.deltaTime = 0, поэтому таймер стоит и стрельба тоже
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;

        Instantiate(laserPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
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
