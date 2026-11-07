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
    public VictoryScreen victoryScreen;
    public GameOverWindow gameOverWindow;
    [Tooltip("Корабль мигает перед окном Game Over")]
    public SpriteRenderer playerSprite;

    public WaveManager waveManager;

    GameManager gm;
    Boss boss;

    void Start()
    {
        gm = GameManager.Instance;
        if (playerSprite == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerSprite = player.GetComponent<SpriteRenderer>();
        }
        gm.StatsChanged += RefreshStats;
        gm.GameEnded += ShowEndScreen;
        waveManager.WaveStarted += OnWaveStarted;
        waveManager.BossSpawned += OnBossSpawned;

        // На экране победы кнопок нет: после ввода имени игра выключается сама (VictoryScreen)
        gameOverWindow.menuButton.onPress.AddListener(gm.LoadMenu);
        gameOverWindow.restartButton.onPress.AddListener(gm.Restart);

        victoryScreen.gameObject.SetActive(false);
        gameOverWindow.gameObject.SetActive(false);
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
        scoreText.text = gm.Score.ToString("00000"); // аркадный счёт с нулями впереди
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

        if (victory)
            victoryScreen.Show(gm);
        else
            gameOverWindow.Show(gm, waveManager, playerSprite);
    }
}
