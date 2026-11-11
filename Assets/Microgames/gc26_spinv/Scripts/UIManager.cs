using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // HUD (очки, жизни, монеты, волна), надпись волны и экран конца игры
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        public TMP_Text scoreText;
        [Tooltip("Сердечки висят под кораблём (его дети) и наклоняются вместе с ним")]
        public SpriteRenderer[] hearts;
        [Tooltip("Прозрачность сердечка потерянной жизни")]
        [Range(0f, 1f)] public float lostHeartAlpha = 0.15f;
        public float heartFadeSpeed = 2.5f;
        public TMP_Text coinsText;
        public TMP_Text waveText;
        public TMP_Text bannerText;

        [Header("Босс")]
        public GameObject bossBar;
        public Image bossBarFill;
        [Tooltip("Белый след за полосой: показывает, сколько только что отняли")]
        public Image bossBarLag;
        public Color bossHealthyColor = new Color(0.22f, 1f, 0.42f);
        public Color bossHurtColor = new Color(1f, 0.83f, 0.35f);
        public Color bossDyingColor = new Color(1f, 0.35f, 0.29f);
        [Tooltip("Через сколько секунд после попадания след начинает догонять полосу")]
        public float lagDelay = 0.3f;
        [Tooltip("С какой скоростью след догоняет полосу (доля полосы в секунду)")]
        public float lagSpeed = 0.6f;

        [Header("Конец игры")]
        public VictoryScreen victoryScreen;
        public GameOverWindow gameOverWindow;
        [Tooltip("Корабль мигает перед окном Game Over")]
        public SpriteRenderer playerSprite;

        public WaveManager waveManager;

        SpinvGameManager gm;
        Boss boss;
        float lagWait;
        float lagValue = 1f; // где сейчас конец белого следа (доля полосы)

        void Start()
        {
            gm = SpinvGameManager.Instance;
            if (playerSprite == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
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
                UpdateBossBar(boss.HealthFraction);
            else if (bossBar.activeSelf)
                bossBar.SetActive(false);
        }

        void UpdateBossBar(float health)
        {
            if (health < bossBarFill.fillAmount)
                lagWait = lagDelay; // новое попадание: след сначала стоит на месте
            bossBarFill.fillAmount = health;
            bossBarFill.color = health > 0.5f
                ? Color.Lerp(bossHurtColor, bossHealthyColor, (health - 0.5f) * 2f)
                : Color.Lerp(bossDyingColor, bossHurtColor, health * 2f);

            if (bossBarLag == null) return;
            if (lagValue <= health)
            {
                lagValue = health;
            }
            else if (lagWait > 0f)
            {
                lagWait -= Time.deltaTime;
            }
            else
            {
                lagValue = Mathf.MoveTowards(lagValue, health, lagSpeed * Time.deltaTime);
            }
            ShowLag(health, lagValue);
        }

        // След рисуется только на отнятом куске (от health до lag), а не под всей полосой:
        // иначе белое из-под зелёного вылезает по краям пурпурной каймой от сдвига цветов CRT
        void ShowLag(float from, float to)
        {
            bool visible = to - from > 0.001f;
            bossBarLag.enabled = visible;
            if (!visible) return;

            var rect = bossBarLag.rectTransform;
            var fill = bossBarFill.rectTransform;
            float width = fill.rect.width;
            rect.offsetMin = new Vector2(fill.offsetMin.x + width * from, rect.offsetMin.y);
            rect.offsetMax = new Vector2(fill.offsetMax.x - width * (1f - to), rect.offsetMax.y);
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
            bossBarFill.color = bossHealthyColor;
            lagValue = 1f;
            if (bossBarLag != null)
            {
                bossBarLag.fillAmount = 1f;
                bossBarLag.enabled = false;
            }
            lagWait = 0f;
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
            // В аркаде волны бесконечные: только номер
            waveText.text = waveManager.ArcadeStyle ? wave.ToString() : $"{wave}/{total}";
            StopAllCoroutines();
            StartCoroutine(ShowBanner(waveManager.BannerText(wave)));
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
}
