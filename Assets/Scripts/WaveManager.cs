using System.Collections;
using UnityEngine;

// Запускает волны по очереди. После последней - победа
public class WaveManager : MonoBehaviour
{
    public WaveData[] waves;
    public InvaderGrid grid;
    [Tooltip("Пауза с надписью «WAVE N» перед появлением пришельцев")]
    public float delayBeforeWave = 2f;
    public float bossSpawnY = 7.5f;

    [Header("Хилка")]
    public HealthPickup healPrefab;
    [Range(0f, 1f)]
    [Tooltip("Шанс, что в волне выпадет хилка. Больше одной за волну не бывает")]
    public float healChance = 0.6f;
    public float healDelayMin = 3f;
    public float healDelayMax = 15f;
    public float healSpawnY = 13.5f;
    public float healEdgePadding = 1f;
    [Tooltip("Сколько хилок гарантированно падает за бой с боссом. Падают равномерно по его ХП: при 3 - на 75%, 50% и 25%")]
    public int bossHeals = 3;

    [Header("Сценка")]
    [Tooltip("Сценка «наступает ночь» перед последней волной. Без неё волна начинается как обычно")]
    public NightCutscene nightCutscene;

    Coroutine healRoutine;

    public int CurrentWave { get; private set; }
    public int TotalWaves => waves.Length;

    // (номер волны, всего волн)
    public event System.Action<int, int> WaveStarted;
    public event System.Action<Boss> BossSpawned;

    public bool IsBossWave(int waveNumber) => waves[waveNumber - 1].bossPrefab != null;

    void Start()
    {
        grid.Cleared += OnWaveCleared;
        grid.ReachedBottom += OnReachedBottom;
        StartCoroutine(RunWave(0));
    }

#if UNITY_EDITOR
    // Только в редакторе: F7 - сразу волна 4 (предпоследняя), F8 - сразу сценка «наступает ночь»
    void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        if (keyboard.f7Key.wasPressedThisFrame)
        {
            ClearForDebug();
            StartCoroutine(RunWave(TotalWaves - 2));
        }
        else if (keyboard.f8Key.wasPressedThisFrame && nightCutscene != null)
        {
            ClearForDebug();
            CurrentWave = TotalWaves - 1;
            WaveStarted?.Invoke(CurrentWave, TotalWaves);
            StartCoroutine(CutsceneThenWave(CurrentWave));
        }
    }

    void ClearForDebug()
    {
        StopAllCoroutines();
        grid.ClearForDebug();
        foreach (Boss boss in FindObjectsByType<Boss>())
            Destroy(boss.gameObject);
    }
#endif

    void OnDestroy()
    {
        if (grid == null) return;
        grid.Cleared -= OnWaveCleared;
        grid.ReachedBottom -= OnReachedBottom;
    }

    // immediate: без паузы с надписью - когда волну запускает сценка за закрытыми веками
    IEnumerator RunWave(int index, bool immediate = false)
    {
        // Ждём один кадр, чтобы UI успел подписаться на события в своём Start()
        yield return null;

        CurrentWave = index + 1;
        WaveStarted?.Invoke(CurrentWave, TotalWaves);

        if (!immediate)
            yield return new WaitForSeconds(delayBeforeWave);
        if (healPrefab == null)
        {
            if (waves[index].bossPrefab != null) SpawnBoss(waves[index].bossPrefab);
            else grid.Build(waves[index]);
            yield break;
        }

        if (waves[index].bossPrefab != null)
        {
            // Бой с боссом: несколько хилок гарантированно, по мере того как босс теряет ХП
            Boss boss = SpawnBoss(waves[index].bossPrefab);
            healRoutine = StartCoroutine(DropBossHeals(boss));
        }
        else
        {
            grid.Build(waves[index]);
            if (Random.value < healChance)
                healRoutine = StartCoroutine(DropHeal());
        }
    }

    Boss SpawnBoss(Boss prefab)
    {
        Boss boss = Instantiate(prefab, new Vector3(0f, bossSpawnY, 0f), Quaternion.identity);
        boss.Init(grid.player);
        boss.Died += OnWaveCleared;
        BossSpawned?.Invoke(boss);
        return boss;
    }

    // Обычная волна: хилка падает в случайный момент
    IEnumerator DropHeal()
    {
        yield return new WaitForSeconds(Random.Range(healDelayMin, healDelayMax));
        SpawnHeal();
    }

    // Босс: хилка номер k падает, когда у босса остаётся 1 - k/(n+1) ХП (при n = 3: 75%, 50%, 25%)
    IEnumerator DropBossHeals(Boss boss)
    {
        for (int k = 1; k <= bossHeals; k++)
        {
            float threshold = 1f - (float)k / (bossHeals + 1);
            while (boss != null && boss.HealthFraction > threshold)
                yield return null;
            if (boss == null || boss.HealthFraction <= 0f) yield break; // босс убит - бой окончен
            SpawnHeal();
        }
    }

    // Хилка падает из случайного места сверху
    void SpawnHeal()
    {
        Camera cam = Camera.main;
        float minX = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x + healEdgePadding;
        float maxX = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x - healEdgePadding;
        Instantiate(healPrefab, new Vector3(Random.Range(minX, maxX), healSpawnY, 0f), Quaternion.identity);
    }

    void OnWaveCleared()
    {
        // Волна кончилась раньше таймера: хилка этой волны уже не выпадет
        if (healRoutine != null)
            StopCoroutine(healRoutine);

        if (CurrentWave >= TotalWaves)
            GameManager.Instance.Victory();
        else if (nightCutscene != null && CurrentWave == TotalWaves - 1)
            StartCoroutine(CutsceneThenWave(CurrentWave));
        else
            StartCoroutine(RunWave(CurrentWave));
    }

    // Перед последней волной - сценка «наступает ночь»; волна стартует за закрытыми веками
    IEnumerator CutsceneThenWave(int index)
    {
        bool started = false;
        yield return nightCutscene.Play(() =>
        {
            started = true;
            StartCoroutine(RunWave(index, true));
        });
        if (!started)
            StartCoroutine(RunWave(index, true));
    }

    void OnReachedBottom()
    {
        GameManager.Instance.GameOver();
    }
}
