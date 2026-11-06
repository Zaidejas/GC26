using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Выключение автомата при выходе: мигает POWER OFF, экран сплющивается в линию, линия - в точку
public class QuitEffect : MonoBehaviour
{
    public float speed = 1f;
    public GameObject content;
    public Image blackScreen;
    public Image screen;
    public TMP_Text message;
    public Image glow;
    public RectTransform topBar;
    public RectTransform bottomBar;
    public RectTransform line;
    public Image dot;

    public Color glowColor = new Color(0.78f, 1f, 0.84f);

    // Этапы на скорости 1, в секундах
    const float PressTime = 0.25f;
    const float MessageEnd = 1.05f;
    const float CollapseTime = 0.3f;
    const float LineTime = 0.25f;
    const float DotTime = 0.3f;
    const float LineThickness = 13f;
    const float DotRadius = 21f;

    System.Action onFinished;
    float time;
    bool playing;

    void Awake()
    {
        content.SetActive(false);
    }

    void OnDisable()
    {
        CrtBoost.Set(0f);
    }

    public void Play(System.Action finished)
    {
        if (playing) return;

        onFinished = finished;
        playing = true;
        time = 0f;
    }

    void Update()
    {
        if (!playing) return;

        time += Time.unscaledDeltaTime * speed;
        if (time < PressTime) return; // Видно нажатую кнопку

        content.SetActive(true);
        CrtBoost.Set(1f);
        Vector2 size = ((RectTransform)transform).rect.size;
        float t = time - PressTime;

        if (t < MessageEnd - PressTime)
        {
            ShowScreen(true, Mathf.FloorToInt(time * 4f) % 2 == 1);
            SetCollapse(0f, size.y);
            return;
        }

        float c = t - (MessageEnd - PressTime);
        if (c < CollapseTime)
        {
            float p = c / CollapseTime;
            ShowScreen(true, true);
            SetCollapse(p, size.y);
            return;
        }

        ShowScreen(false, false);
        c -= CollapseTime;
        if (c < LineTime)
        {
            float p = c / LineTime;
            line.gameObject.SetActive(true);
            line.sizeDelta = new Vector2(Mathf.Max(4f, size.x * (1f - p)), LineThickness);
            return;
        }

        line.gameObject.SetActive(false);
        c -= LineTime;
        if (c < DotTime)
        {
            float p = c / DotTime;
            dot.gameObject.SetActive(true);
            float d = (DotRadius * (1f - p) + 5f) * 2f;
            dot.rectTransform.sizeDelta = new Vector2(d, d);
            Color color = glowColor;
            color.a = 1f - p;
            dot.color = color;
            return;
        }

        dot.gameObject.SetActive(false);
        playing = false;
        onFinished?.Invoke();
    }

    void ShowScreen(bool on, bool messageVisible)
    {
        screen.gameObject.SetActive(on);
        message.gameObject.SetActive(on && messageVisible);
        glow.gameObject.SetActive(on);
        topBar.gameObject.SetActive(on);
        bottomBar.gameObject.SetActive(on);
        line.gameObject.SetActive(false);
        dot.gameObject.SetActive(false);
        blackScreen.gameObject.SetActive(true);
    }

    // Чёрные полосы сверху и снизу сжимают картинку в линию, остаток светлеет
    void SetCollapse(float p, float height)
    {
        float band = Mathf.Max(LineThickness, height * (1f - p * p));
        float bar = (height - band) / 2f;
        topBar.sizeDelta = new Vector2(0f, bar);
        bottomBar.sizeDelta = new Vector2(0f, bar);
        Color color = glowColor;
        color.a = 0.8f * p;
        glow.color = color;
    }
}
