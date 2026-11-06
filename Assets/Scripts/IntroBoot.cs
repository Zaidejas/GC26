using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Включение аркадного автомата при запуске игры: помехи, строки самопроверки и TAP TO START.
// Меню открывается только после касания. Показывается один раз за запуск игры
public class IntroBoot : MonoBehaviour
{
    [Tooltip("1 - как в предпросмотре на 100%, 0.5 - вдвое медленнее")]
    public float speed = 0.5f;
    public CanvasGroup group;
    public Image background;
    public Image[] bars;
    public RawImage noise;
    [Tooltip("Строки самопроверки, последняя - TAP TO START")]
    public TMP_Text[] lines;

    public Color blackColor = Color.black;
    public Color screenColor = new Color(0.012f, 0.063f, 0.024f);
    public Color barColor = new Color(0.22f, 1f, 0.42f);

    // Время этапов на скорости 1, в секундах
    const float StaticStart = 0.05f;
    const float BootStart = 0.55f;
    const float BootEnd = 1.9f;
    const float FadeTime = 0.35f;
    const float ScreenHeight = 2778f;

    static bool shownThisLaunch;

    // В редакторе включён вход в Play без перезагрузки домена, и статические поля
    // переживают перезапуск Play. Сбрасываем отметку при каждом новом запуске
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShown()
    {
        shownThisLaunch = false;
    }

    enum Stage { Static, Boot, WaitTap, Fade }

    Stage stage = Stage.Static;
    float time;
    float fadeTime;
    bool wasPressed;
    Texture2D noiseTexture;
    Color32[] noisePixels;

    void Awake()
    {
        if (shownThisLaunch)
        {
            gameObject.SetActive(false);
            return;
        }
        shownThisLaunch = true;

        noiseTexture = new Texture2D(160, 346, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        noisePixels = new Color32[noiseTexture.width * noiseTexture.height];
        noise.texture = noiseTexture;
        ShowLines(0, false);
    }

    void OnDestroy()
    {
        if (noiseTexture != null) Destroy(noiseTexture);
    }

    void OnDisable()
    {
        CrtBoost.Set(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * speed;
        time += dt;
        bool tap = TapPressed();
        CrtBoost.Set(stage == Stage.Fade ? group.alpha : 1f);

        if (stage == Stage.Fade)
        {
            fadeTime += dt;
            group.alpha = 1f - Mathf.Clamp01(fadeTime / FadeTime);
            if (fadeTime >= FadeTime) gameObject.SetActive(false);
            return;
        }

        if (tap)
        {
            // Касание во время загрузки досматривает её до конца, касание на TAP TO START - запускает меню
            if (stage == Stage.WaitTap)
            {
                // Надписи пропадают сразу, плавно уходит только тёмный экран
                ShowLines(0, false);
                stage = Stage.Fade;
                return;
            }
            stage = Stage.WaitTap;
            time = BootEnd;
        }

        if (time < StaticStart) DrawBlack();
        else if (time < BootStart && stage == Stage.Static) DrawStatic((time - StaticStart) / (BootStart - StaticStart));
        else if (time < BootEnd && stage != Stage.WaitTap) DrawBoot((time - BootStart) / (BootEnd - BootStart));
        else
        {
            stage = Stage.WaitTap;
            DrawPrompt();
        }
    }

    bool TapPressed()
    {
        bool pressed = GameInput.PointerHeld(out _);
        bool tap = pressed && !wasPressed;
        wasPressed = pressed;
        return tap;
    }

    void DrawBlack()
    {
        background.color = blackColor;
        SetBars(false);
        noise.enabled = false;
    }

    // Полосы помех и шум, который постепенно гаснет
    void DrawStatic(float p)
    {
        background.color = blackColor;
        SetBars(true);
        for (int i = 0; i < bars.Length; i++)
        {
            var rect = bars[i].rectTransform;
            float y = (i * 137f + time * 900f) % 650f / 650f * ScreenHeight;
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(0f, (8f + i * 3f) / 650f * ScreenHeight);
            Color c = barColor;
            c.a = 0.25f + 0.25f * Mathf.Sin(i);
            bars[i].color = c;
        }

        noise.enabled = true;
        byte alpha = (byte)(255 * 0.5f * (1f - p));
        for (int i = 0; i < noisePixels.Length; i++)
            noisePixels[i] = Random.value < 0.08f ? new Color32(200, 255, 214, alpha) : new Color32(0, 0, 0, 0);
        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply();
    }

    // Строки самопроверки появляются по одной
    void DrawBoot(float p)
    {
        stage = Stage.Boot;
        background.color = screenColor;
        SetBars(false);
        noise.enabled = false;
        ShowLines(Mathf.FloorToInt(p * lines.Length * 1.2f), true);
    }

    void DrawPrompt()
    {
        background.color = screenColor;
        SetBars(false);
        noise.enabled = false;
        ShowLines(lines.Length, Mathf.FloorToInt(time * 4f) % 2 == 1);
    }

    // Последняя строка (TAP TO START) мигает
    void ShowLines(int count, bool promptVisible)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            bool isPrompt = i == lines.Length - 1;
            lines[i].gameObject.SetActive(i < count && (!isPrompt || promptVisible));
        }
    }

    void SetBars(bool on)
    {
        foreach (Image bar in bars) bar.enabled = on;
    }
}
