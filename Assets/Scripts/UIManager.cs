using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD (очки, жизни, монеты, волна), надпись волны и экран конца игры
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public TMP_Text scoreText;
    public Image[] hearts;
    [Tooltip("Прозрачность сердечка потерянной жизни")]
    [Range(0f, 1f)] public float lostHeartAlpha = 0.15f;
    public float heartFadeSpeed = 2.5f;
    public TMP_Text coinsText;
    public TMP_Text waveText;
    public TMP_Text bannerText;

    [Header("Босс")]
    public GameObject bossBar;
    public Image bossBarFill;

    [Header("Конец игры")]
    public GameObject endPanel;
    public TMP_Text endTitleText;
    public TMP_Text endStatsText;
    public Button restartButton;
    public Button menuButton;

    public WaveManager waveManager;

    GameManager gm;
    Boss boss;

    void Start()
    {
        gm = GameManager.Instance;
        gm.StatsChanged += RefreshStats;
        gm.GameEnded += ShowEndScreen;
        waveManager.WaveStarted += OnWaveStarted;
        waveManager.BossSpawned += OnBossSpawned;

        restartButton.onClick.AddListener(gm.Restart);
        menuButton.onClick.AddListener(gm.LoadMenu);

        endPanel.SetActive(false);
        bannerText.gameObject.SetActive(false);
        bossBar.SetActive(false);
        RefreshStats();
    }

    void Update()
    {
        UpdateHearts();

        if (boss != null)
            bossBarFill.fillAmount = boss.HealthFraction;
        else if (bossBar.activeSelf)
            bossBar.SetActive(false);
    }

    // Сердечки плавно затухают при потере жизни и проявляются при лечении.
    // Реальное время: последнее сердечко должно погаснуть и на экране конца игры
    void UpdateHearts()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            Color color = hearts[i].color;
            float target = i < gm.Lives ? 1f : lostHeartAlpha;
            color.a = Mathf.MoveTowards(color.a, target, heartFadeSpeed * Time.unscaledDeltaTime);
            hearts[i].color = color;
        }
    }

    void OnBossSpawned(Boss spawned)
    {
        boss = spawned;
        bossBarFill.fillAmount = 1f;
        bossBar.SetActive(true);
    }

    void OnDestroy()
    {
        if (gm != null)
        {
            gm.StatsChanged -= RefreshStats;
            gm.GameEnded -= ShowEndScreen;
        }
        if (waveManager != null)
        {
            waveManager.WaveStarted -= OnWaveStarted;
            waveManager.BossSpawned -= OnBossSpawned;
        }
    }

    void RefreshStats()
    {
        scoreText.text = gm.Score.ToString();
        coinsText.text = gm.Coins.ToString();
    }

    void OnWaveStarted(int wave, int total)
    {
        waveText.text = $"{wave}/{total}";
        StopAllCoroutines();
        StartCoroutine(ShowBanner(BannerText(wave, total)));
    }

    string BannerText(int wave, int total)
    {
        if (waveManager.IsBossWave(wave))
            return wave == total ? "FINAL BOSS" : "BOSS";
        return wave == total ? "FINAL WAVE" : $"WAVE {wave}";
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
        endTitleText.color = victory ? new Color(0.78f, 1f, 0.84f) : new Color(0.13f, 0.76f, 0.29f);
        endStatsText.text =
            $"SCORE  {gm.Score}\n" +
            $"BEST  {gm.HighScore}\n" +
            $"COINS  +{gm.Coins}   (TOTAL {gm.TotalCoins})";
    }
}
