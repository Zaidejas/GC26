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
    [HideInInspector] public float fireRateMultiplier = 1f;

    [Header("Щит")]
    public GameObject shieldVisual;

    [Header("Урон")]
    public float invulnerableTime = 1.5f;

    // Куда может встать центр корабля и его полуширина по коллайдеру: по ним босс оставляет проход в стене
    public float MinX => minX;
    public float MaxX => maxX;
    public float HalfWidth => GetComponent<Collider2D>().bounds.extents.x;

    SpriteRenderer spriteRenderer;
    Camera cam;
    float minX, maxX;
    float fireTimer;
    bool invulnerable;
    bool shielded;
    bool dragging;
    bool wasPressed;

    void Start()
    {
        SetShield(false);
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

        // Если палец поставили мимо интерфейса, им можно вести корабль и поверх панели.
        // Нажатие, начатое на кнопке, корабль не двигает
        bool pressed = GameInput.PointerHeld(out Vector2 screenPosition);
        if (pressed && !wasPressed)
            dragging = !IsOverUI();
        wasPressed = pressed;

        if (pressed && dragging)
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
        fireTimer = fireCooldown / fireRateMultiplier;
    }

    public void SetShield(bool on)
    {
        shielded = on;
        if (shieldVisual != null)
            shieldVisual.SetActive(on);
    }

    // Вызывается ракетой пришельца
    public void Hit()
    {
        if (shielded || invulnerable || GameManager.Instance.IsGameOver) return;

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
