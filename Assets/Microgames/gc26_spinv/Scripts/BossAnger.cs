using UnityEngine;

namespace GC26.Spinv
{
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
        public float shakeAmount = 0.04f;
        [Tooltip("Наклон на пике, градусов")]
        public float shakeAngle = 5f;
        [Tooltip("Как часто босс перекидывается из стороны в сторону, секунд на рывок")]
        public float shakeStepTime = 0.07f;
        [Tooltip("Небольшой разброс поверх рывков, в долях сдвига")]
        [Range(0f, 1f)] public float shakeJitter = 0.25f;

        [Header("Попадание")]
        [Range(0f, 1f)] public float hitRedness = 0.4f;
        public float hitFlashTime = 0.25f;

        [Header("Звук приступа")]
        [Tooltip("Играет на время приступа и затухает к его концу")]
        public AudioClip fitSound;
        [Range(0f, 1f)] public float fitSoundVolume = 0.3f;
        AudioSource fitSource;

        void StartFitSound()
        {
            if (fitSound == null) return;
            if (fitSource == null)
            {
                fitSource = gameObject.AddComponent<AudioSource>();
                fitSource.playOnAwake = false;
            }
            fitSource.clip = fitSound;
            fitSource.volume = fitSoundVolume * SpinvAudioSettings.SfxVolume;
            fitSource.Play();
        }

        // Звук приступа: с fitSoundVolume плавно до тишины ровно к концу приступа
        void UpdateFitSound()
        {
            if (fitSource == null || !fitSource.isPlaying) return;
            if (fitTime < 0f || fitTime >= fitDuration)
            {
                fitSource.Stop();
                return;
            }
            fitSource.volume = fitSoundVolume * SpinvAudioSettings.SfxVolume * (1f - fitTime / fitDuration);
        }

        // Анимация смерти (Boss.DramaticDeath): >= 0 - босс полностью красный и трясётся с этой силой
        [HideInInspector] public float dyingShake = -1f;

        Boss boss;
        float untilFit;
        float fitTime = -1f;
        float hitFlash;
        float lastHealth;
        float visualWidth;
        float stepTimer;
        float side = 1f; // в какую сторону сейчас качнулся: -1 влево, 1 вправо

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
                if (untilFit <= 0f)
                {
                    fitTime = 0f;
                    StartFitSound();
                }
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
            shake += hitFlash * 0.25f;
            if (dyingShake >= 0f)
            {
                red = 1f;
                shake = dyingShake;
            }

            // Рывки: через равные промежутки босс резко перекидывается в другую сторону
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = shakeStepTime;
                side = -side;
            }

            UpdateFitSound();
            Apply(red, shake);
        }

        void Apply(float red, float shake)
        {
            if (visual != null)
            {
                // Качается туда-сюда без плавного перехода: сдвиг и наклон в сторону side,
                // сверху небольшой разброс, чтобы рывки не были механически одинаковыми
                float a = shake * shakeAmount * visualWidth;
                float x = side * a + Random.Range(-a, a) * shakeJitter;
                float y = Random.Range(-a, a) * 0.3f;
                visual.localPosition = new Vector3(x, y, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, -side * shake * shakeAngle);
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
}
