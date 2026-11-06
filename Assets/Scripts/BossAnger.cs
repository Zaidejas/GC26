using UnityEngine;

// «Приступы ярости» босса: раз в несколько секунд (чаще при низком ХП) он наливается красным,
// сильно трясётся и остывает. Попадания дают лёгкую красную вспышку.
// Трясётся только картинка (дочерний Visual), коллайдер и движение босса не сдвигаются.
// Красный цвет - отдельный красный спрайт поверх обычного, его прозрачность = сила гнева
[RequireComponent(typeof(Boss))]
public class BossAnger : MonoBehaviour
{
    public Transform visual;
    public SpriteRenderer body;
    public SpriteRenderer angry;

    [Header("Приступы")]
    [Tooltip("Первый приступ после появления босса, секунд")]
    public float firstFitDelay = 3f;
    [Tooltip("Пауза между приступами при полном ХП, секунд")]
    public float intervalFullHealth = 5f;
    [Tooltip("Пауза между приступами при почти нулевом ХП, секунд")]
    public float intervalMin = 2f;
    [Tooltip("Длина приступа целиком, секунд")]
    public float fitDuration = 1.1f;
    [Tooltip("Сколько из приступа он на пике (красный и сильная тряска), секунд")]
    public float fitPeak = 0.6f;
    [Range(0f, 1f)] public float fitRedness = 0.9f;

    [Header("Тряска")]
    [Tooltip("Сдвиг картинки на пике, в долях её ширины")]
    public float shakeAmount = 0.035f;
    [Tooltip("Наклон на пике, градусов")]
    public float shakeAngle = 3f;

    [Header("Попадание")]
    [Range(0f, 1f)] public float hitRedness = 0.4f;
    public float hitFlashTime = 0.25f;

    Boss boss;
    float untilFit;
    float fitTime = -1f;
    float hitFlash;
    float lastHealth;
    float visualWidth;

    void Awake()
    {
        boss = GetComponent<Boss>();
    }

    void Start()
    {
        untilFit = firstFitDelay;
        lastHealth = boss.HealthFraction;
        // Ширина в локальных единицах Visual: масштаб босса применится сам
        visualWidth = body != null && body.sprite != null ? body.sprite.bounds.size.x : 1f;
        Apply(0f, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // пауза: картинка замирает как есть

        float health = boss.HealthFraction;
        if (health < lastHealth) hitFlash = 1f;
        lastHealth = health;
        hitFlash = Mathf.Max(0f, hitFlash - dt / hitFlashTime);

        float damage = 1f - Mathf.Clamp01(health);
        if (fitTime < 0f)
        {
            untilFit -= dt;
            if (untilFit <= 0f) fitTime = 0f;
        }

        float fit = 0f;   // 0..1 сила красного от приступа
        float shake = 0f; // 0..1 сила тряски
        if (fitTime >= 0f)
        {
            fitTime += dt;
            if (fitTime >= fitDuration)
            {
                fitTime = -1f;
                untilFit = Mathf.Lerp(intervalFullHealth, intervalMin, damage);
            }
            else if (fitTime <= fitPeak)
            {
                fit = 1f;
                shake = 1f;
            }
            else
            {
                // Остывает: краснота и тряска плавно уходят
                fit = 1f - (fitTime - fitPeak) / (fitDuration - fitPeak);
                shake = fit * 0.45f;
            }
        }

        float red = Mathf.Max(fit * fitRedness, hitFlash * hitRedness);
        shake += hitFlash * 0.2f;
        Apply(red, shake);
    }

    void Apply(float red, float shake)
    {
        if (visual != null)
        {
            float a = shake * shakeAmount * visualWidth;
            visual.localPosition = new Vector3(Random.Range(-a, a), Random.Range(-a, a) * 0.5f, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1f, 1f) * shake * shakeAngle);
        }

        if (angry != null && body != null)
        {
            // Повторяем оттенок основного спрайта (вспышка попадания, ярость на половине ХП)
            Color c = body.color;
            c.a = red;
            angry.color = c;
        }
    }
}
