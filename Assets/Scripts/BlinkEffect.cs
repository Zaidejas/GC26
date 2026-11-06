using UnityEngine;

// «Уставший игрок»: на финальном боссе экран время от времени быстро моргает.
// Веки лежат на отдельном Canvas поверх экрана, поэтому CRT-эффект их не трогает
public class BlinkEffect : MonoBehaviour
{
    public WaveManager waveManager;
    public RectTransform topLid;
    public RectTransform bottomLid;
    public RectTransform topCurve;
    public RectTransform bottomCurve;

    [Tooltip("Раз во сколько секунд моргает")]
    public float interval = 10f;
    [Tooltip("Длина одного моргания, в секундах")]
    public float duration = 0.29f;
    [Range(0f, 1f)]
    [Tooltip("Насколько закрывается экран: 1 - полностью")]
    public float closeAmount = 1f;
    [Tooltip("Первое моргание после появления босса")]
    public float firstDelay = 3f;
    [Tooltip("Высота изгиба века в единицах Canvas")]
    public float curveHeight = 170f;

    Boss boss;
    float timer;
    float blinkTime = -1f;

    void Start()
    {
        waveManager.BossSpawned += OnBossSpawned;
        SetClosed(0f);
    }

    void OnDestroy()
    {
        if (waveManager != null)
            waveManager.BossSpawned -= OnBossSpawned;
    }

    void OnBossSpawned(Boss spawned)
    {
        if (!spawned.playerBlinks) return;

        boss = spawned;
        timer = firstDelay;
    }

    void Update()
    {
        // На паузе и после конца игры веки не должны закрывать меню
        if (boss == null || Time.timeScale == 0f)
        {
            SetClosed(0f);
            if (boss == null) blinkTime = -1f;
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            blinkTime = 0f;
            timer = interval;
        }

        if (blinkTime < 0f) return;

        blinkTime += Time.deltaTime;
        float p = blinkTime / duration;
        if (p >= 1f)
        {
            blinkTime = -1f;
            SetClosed(0f);
            return;
        }

        // Быстро закрылись, чуть задержались, открылись чуть медленнее
        float k = p < 0.35f ? p / 0.35f : p < 0.55f ? 1f : 1f - (p - 0.55f) / 0.45f;
        SetClosed(k);
    }

    void SetClosed(float k)
    {
        float half = ((RectTransform)transform).rect.height / 2f;
        float cover = k * closeAmount * half;
        topLid.sizeDelta = new Vector2(0f, cover);
        bottomLid.sizeDelta = new Vector2(0f, cover);
        topCurve.sizeDelta = new Vector2(0f, curveHeight * k);
        bottomCurve.sizeDelta = new Vector2(0f, curveHeight * k);
    }
}
