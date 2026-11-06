using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Окно Game Over поверх замершей игры: корабль мигает, экран темнеет, окно раскрывается,
// затем все надписи печатаются по очереди с курсором «_». Кнопки работают, когда напечатано всё.
// Реальное время: игра стоит на Time.timeScale = 0
public class GameOverWindow : MonoBehaviour
{
    public Image dimmer;
    [Range(0f, 1f)] public float dimAlpha = 0.72f;
    public RectTransform window;

    [Header("Надписи (печатаются в этом порядке)")]
    public TMP_Text titleTop;
    public TMP_Text titleBottom;
    public TMP_Text scoreLabel, scoreValue;
    public TMP_Text waveLabel, waveValue;
    public TMP_Text coinsLabel, coinsValue;
    public TMP_Text bestLabel, bestValue;
    public TMP_Text newHighScore;
    public TMP_Text insertCoin;

    [Header("Кнопки")]
    public PressableButton menuButton;
    public PressableButton restartButton;

    [Header("Скорость")]
    public float titleCharTime = 0.07f;
    public float charTime = 0.035f;
    public float pauseBetweenLines = 0.09f;

    struct Line
    {
        public TMP_Text text;
        public string value;
        public float charTime;
        public GameObject showFirst; // рамка кнопки появляется перед печатью её надписи
    }

    public void Show(GameManager gm, WaveManager waves, SpriteRenderer playerSprite)
    {
        gameObject.SetActive(true);

        var lines = new List<Line>
        {
            new Line { text = titleTop, value = "GAME", charTime = titleCharTime },
            new Line { text = titleBottom, value = "OVER", charTime = titleCharTime },
            new Line { text = scoreLabel, value = "SCORE" },
            new Line { text = scoreValue, value = gm.Score.ToString("00000") },
            new Line { text = waveLabel, value = "WAVE" },
            new Line { text = waveValue, value = $"{waves.CurrentWave} / {waves.TotalWaves}" },
            new Line { text = coinsLabel, value = "COINS" },
            new Line { text = coinsValue, value = $"+{gm.Coins}" },
            new Line { text = bestLabel, value = "BEST" },
            new Line { text = bestValue, value = Mathf.Max(gm.PreviousBest, gm.Score).ToString("00000") },
        };
        if (gm.Score > gm.PreviousBest)
            lines.Add(new Line { text = newHighScore, value = "NEW HIGH SCORE!" });
        lines.Add(new Line { text = menuButton.label, value = "MENU", showFirst = menuButton.gameObject });
        lines.Add(new Line { text = restartButton.label, value = "RESTART", showFirst = restartButton.gameObject });
        lines.Add(new Line { text = insertCoin, value = "INSERT COIN" });

        foreach (TMP_Text text in new[] { titleTop, titleBottom, scoreLabel, scoreValue, waveLabel, waveValue,
                     coinsLabel, coinsValue, bestLabel, bestValue, newHighScore, insertCoin, menuButton.label, restartButton.label })
            text.text = "";
        SetBlink(newHighScore, false);
        SetBlink(insertCoin, false);
        menuButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        menuButton.interactable = restartButton.interactable = false;
        SetDim(0f);
        window.localScale = new Vector3(1f, 0f, 1f);

        StartCoroutine(Play(lines, playerSprite));
    }

    IEnumerator Play(List<Line> lines, SpriteRenderer playerSprite)
    {
        // Корабль мигает три раза
        for (int i = 0; i < 6; i++)
        {
            if (playerSprite != null) playerSprite.enabled = i % 2 == 1;
            yield return new WaitForSecondsRealtime(0.085f);
        }
        if (playerSprite != null) playerSprite.enabled = true;

        // Экран темнеет, окно раскрывается с лёгким отскоком
        const float openTime = 0.3f, dimTime = 0.4f;
        for (float t = 0f; t < dimTime; t += Time.unscaledDeltaTime)
        {
            SetDim(t / dimTime);
            float k = Mathf.Clamp01(t / openTime);
            window.localScale = new Vector3(1f, BackOut(k), 1f);
            yield return null;
        }
        SetDim(1f);
        window.localScale = Vector3.one;

        foreach (Line line in lines)
        {
            if (line.showFirst != null) line.showFirst.SetActive(true);
            float speed = line.charTime > 0f ? line.charTime : charTime;
            for (int k = 1; k <= line.value.Length; k++)
            {
                line.text.text = line.value.Substring(0, k) + "_";
                yield return new WaitForSecondsRealtime(speed);
            }
            line.text.text = line.value;
            yield return new WaitForSecondsRealtime(pauseBetweenLines);
        }

        SetBlink(newHighScore, true);
        SetBlink(insertCoin, true);
        menuButton.interactable = restartButton.interactable = true;
    }

    // Пружинка: чуть перелетает за 1 и возвращается
    static float BackOut(float t)
    {
        const float s = 1.7f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    void SetDim(float k)
    {
        Color c = dimmer.color;
        c.a = dimAlpha * Mathf.Clamp01(k);
        dimmer.color = c;
    }

    static void SetBlink(TMP_Text text, bool on)
    {
        var blink = text.GetComponent<BlinkGraphic>();
        if (blink != null) blink.enabled = on;
    }
}
