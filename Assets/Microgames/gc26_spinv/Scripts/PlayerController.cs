using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GC26.Spinv
{
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
        // Сценки выключают стрельбу: «наступает ночь» и смерть финального босса
        [HideInInspector] public bool canShoot = true;

        [Header("Щит")]
        public GameObject shieldVisual;
        // Сколько ещё держится щит: по нему купол мигает перед концом
        [HideInInspector] public float shieldTimeLeft;

        // Луч Phantom-а делает корабль «вязким»: доля обычной скорости
        [HideInInspector] public float moveMultiplier = 1f;

        [Header("Урон")]
        public float invulnerableTime = 1.5f;

        // Куда может встать центр корабля и его полуширина по картинке (не по уменьшенному хитбоксу):
        // по ним босс оставляет проход в стене, визуально шире корабля
        public float MinX => minX;
        public float MaxX => maxX;
        public float HalfWidth => GetComponent<SpriteRenderer>().bounds.extents.x;

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

        void OnEnable() => GameInput.EnableTouch();
        void OnDisable() => GameInput.DisableTouch();

        void Update()
        {
            if (SpinvGameManager.Instance != null && SpinvGameManager.Instance.IsGameOver) return;

            Move();
            AutoFire();
        }

        void Move()
        {
            Vector3 pos = transform.position;
            pos.x += GameInput.Horizontal() * speed * moveMultiplier * Time.deltaTime;

            // Если палец поставили мимо интерфейса, им можно вести корабль и поверх панели.
            // Нажатие, начатое на кнопке, корабль не двигает
            bool pressed = GameInput.PointerHeld(out Vector2 screenPosition);
            if (pressed && !wasPressed)
                dragging = !IsOverUI(screenPosition);
            wasPressed = pressed;

            if (pressed && dragging)
            {
                float targetX = cam.ScreenToWorldPoint(screenPosition).x;
                pos.x = Mathf.MoveTowards(pos.x, targetX, followSpeed * moveMultiplier * Time.deltaTime);
            }

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            transform.position = pos;
        }

        // Нажатие на кнопку интерфейса не должно двигать корабль.
        // Проверяем лучом ровно в точке касания: IsPointerOverGameObject() в первый кадр касания
        // отвечает по прошлому касанию (UI ещё не обработал новое) - после нажатия кнопки
        // следующее касание по полю считалось «по кнопке», и корабль вставал до следующего касания
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        static bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPosition }, uiHits);
            return uiHits.Count > 0;
        }

        void AutoFire()
        {
            if (!canShoot) return;

            // На паузе Time.deltaTime = 0, поэтому таймер стоит и стрельба тоже
            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f) return;

            Instantiate(laserPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
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
            if (shielded || invulnerable || SpinvGameManager.Instance.IsGameOver) return;

            SpinvSoundManager.Play(Sfx.PlayerHit);
            SpinvGameManager.Instance.LoseLife();

            if (!SpinvGameManager.Instance.IsGameOver)
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
}
