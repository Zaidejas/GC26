using System.Collections;
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

    // Чем меньше ХП у босса, тем чаще и дольше моргание (линейно от полного ХП к нулю)
    [Tooltip("Интервал между морганиями при полном ХП босса, секунд")]
    public float intervalFullHealth = 10f;
    [Tooltip("Интервал при почти нулевом ХП - чаще не бывает, секунд")]
    public float intervalMin = 6f;
    [Tooltip("Длина моргания при полном ХП босса, секунд")]
    public float durationFullHealth = 0.29f;
    [Tooltip("Длина моргания при почти нулевом ХП - дольше не бывает, секунд")]
    public float durationMax = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("Насколько закрывается экран: 1 - полностью")]
    public float closeAmount = 1f;
    [Tooltip("Первое моргание после появления босса")]
    public float firstDelay = 3f;
    [Tooltip("Высота изгиба века в единицах Canvas")]
    public float curveHeight = 170f;

    // Размытие поверх CRT (под веками): начинается вместе с морганием, после открытия глаз
    // висит в полную силу blurHold и быстро уходит за blurFadeTime.
    // Чем длиннее моргание, тем дольше висит (пропорционально длине моргания)
    [Header("Размытие")]
    [Tooltip("Сколько размытие висит после открытия глаз при обычном моргании, секунд")]
    public float blurHold = 0.3f;
    [Tooltip("За сколько размытие уходит после того, как провисело, секунд")]
    public float blurFadeTime = 0.15f;
    [Tooltip("Радиус размытия на пике, в долях высоты экрана")]
    public float blurRadius = 0.0105f;

    Boss boss;
    float sinceLastBlink;
    bool firstBlinkPending;
    float blinkTime = -1f;
    float duration;
    float blurTime = -1f;
    float blurHoldNow;

    // 0 - у босса полное ХП, 1 - почти убит
    float Damage => boss == null ? 0f : 1f - Mathf.Clamp01(boss.HealthFraction);
    float CurrentInterval => Mathf.Lerp(intervalFullHealth, intervalMin, Damage);

    void Start()
    {
        waveManager.BossSpawned += OnBossSpawned;
        SetClosed(0f);
        CrtBoost.SetBlur(0f);
    }

    void OnDestroy()
    {
        if (waveManager != null)
            waveManager.BossSpawned -= OnBossSpawned;
        // Глобальная настройка шейдера переживает смену сцены - сбрасываем
        CrtBoost.SetBlur(0f);
    }

    void OnBossSpawned(Boss spawned)
    {
        if (!spawned.playerBlinks) return;

        boss = spawned;
        sinceLastBlink = 0f;
        firstBlinkPending = true;
    }

    // Сценка (NightCutscene) сама управляет веками и размытием
    bool scripted;

    // Долгое моргание для сценки: веки закрываются за 30% времени, держатся до 60%, открываются.
    // Размытие нарастает вместе с веками и уходит за blurFadeTime после открытия.
    // onClosed вызывается, когда глаза полностью закрыты - в этот момент сценка меняет картинку
    public IEnumerator LongBlink(float length, System.Action onClosed)
    {
        scripted = true;
        bool closedFired = false;
        for (float t = 0f; t < length; )
        {
            if (Time.timeScale == 0f)
            {
                // Пауза: меню паузы не должно оказаться под закрытыми веками
                SetClosed(0f);
                CrtBoost.SetBlur(0f);
                yield return null;
                continue;
            }
            float p = t / length;
            SetClosed(p < 0.3f ? p / 0.3f : p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f);
            CrtBoost.SetBlur(blurRadius * Mathf.Min(1f, p / 0.3f));
            if (!closedFired && p >= 0.3f)
            {
                closedFired = true;
                onClosed?.Invoke();
            }
            yield return null;
            t += Time.deltaTime;
        }
        if (!closedFired) onClosed?.Invoke();
        SetClosed(0f);

        for (float t = 0f; t < blurFadeTime; t += Time.deltaTime)
        {
            CrtBoost.SetBlur(Time.timeScale == 0f ? 0f : blurRadius * (1f - t / blurFadeTime));
            yield return null;
        }
        CrtBoost.SetBlur(0f);
        scripted = false;
    }

    void Update()
    {
        if (scripted) return;

        // На паузе и после конца игры веки не должны закрывать меню
        // После победы над боссом (уже во время анимации смерти) моргание прекращается
        if (boss == null || boss.IsDead || Time.timeScale == 0f)
        {
            SetClosed(0f);
            CrtBoost.SetBlur(0f);
            if (boss == null || boss.IsDead) blinkTime = blurTime = -1f;
            return;
        }

        UpdateBlur();

        // Интервал пересчитывается каждый кадр: урон по боссу сразу приближает следующее моргание
        sinceLastBlink += Time.deltaTime;
        float wait = firstBlinkPending ? firstDelay : CurrentInterval;
        if (sinceLastBlink >= wait)
        {
            blinkTime = 0f;
            sinceLastBlink = 0f;
            firstBlinkPending = false;
            // Длину фиксируем на старте, чтобы моргание не дёргалось от попаданий
            duration = Mathf.Lerp(durationFullHealth, durationMax, Damage);
            // Размытие стартует вместе с морганием, висит тем дольше, чем длиннее моргание
            blurTime = 0f;
            blurHoldNow = blurHold * duration / Mathf.Max(0.01f, durationFullHealth);
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

    // Нарастает, пока веки закрываются, держится до открытия и ещё blurHoldNow, потом быстро уходит
    void UpdateBlur()
    {
        if (blurTime < 0f) return;
        blurTime += Time.deltaTime;

        float amount;
        float holdEnd = duration + blurHoldNow;
        if (blurTime < duration)
        {
            float p = blurTime / duration;
            amount = p < 0.35f ? p / 0.35f : 1f;
        }
        else if (blurTime < holdEnd)
        {
            amount = 1f; // глаза открылись, а картинка ещё мутная
        }
        else
        {
            float x = (blurTime - holdEnd) / Mathf.Max(0.01f, blurFadeTime);
            if (x >= 1f)
            {
                blurTime = -1f;
                CrtBoost.SetBlur(0f);
                return;
            }
            amount = 1f - x;
        }
        CrtBoost.SetBlur(blurRadius * amount);
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
