using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD (очки, жизни, монеты, волна), надпись волны и экран конца игры
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text livesText;
    public TMP_Text coinsText;
    public TMP_Text waveText;
    public TMP_Text bannerText;

    [Header("Конец игры")]
    public GameObject endPanel;
    public TMP_Text endTitleText;
    public TMP_Text endStatsText;
    public Button restartButton;
    public Button menuButton;

    public WaveManager waveManager;

    GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
        gm.StatsChanged += RefreshStats;
        gm.GameEnded += ShowEndScreen;
        waveManager.WaveStarted += OnWaveStarted;

        restartButton.onClick.AddListener(gm.Restart);
        menuButton.onClick.AddListener(gm.LoadMenu);

        endPanel.SetActive(false);
        bannerText.gameObject.SetActive(false);
        RefreshStats();
    }

    void OnDestroy()
    {
        if (gm != null)
        {
            gm.StatsChanged -= RefreshStats;
            gm.GameEnded -= ShowEndScreen;
        }
        if (waveManager != null)
            waveManager.WaveStarted -= OnWaveStarted;
    }

    void RefreshStats()
    {
        scoreText.text = $"SCORE {gm.Score}";
        livesText.text = $"LIVES {gm.Lives}";
        coinsText.text = $"COINS {gm.Coins}";
    }

    void OnWaveStarted(int wave, int total)
    {
        waveText.text = $"WAVE {wave}/{total}";
        StopAllCoroutines();
        StartCoroutine(ShowBanner(wave == total ? "FINAL WAVE" : $"WAVE {wave}"));
    }

    IEnumerator ShowBanner(string text)
    {
        bannerText.text = text;
        bannerText.gameObject.SetActive(true);
        yield return new WaitForSeconds(waveManager.delayBeforeWave);
        bannerText.gameObject.SetActive(false);
    }

    void ShowEndScreen(bool victory)
    {
        StopAllCoroutines();
        bannerText.gameObject.SetActive(false);
        endPanel.SetActive(true);

        endTitleText.text = victory ? "YOU WIN!" : "GAME OVER";
        endTitleText.color = victory ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
        endStatsText.text =
            $"SCORE  {gm.Score}\n" +
            $"BEST  {gm.HighScore}\n" +
            $"COINS  +{gm.Coins}   (TOTAL {gm.TotalCoins})";
    }
}
