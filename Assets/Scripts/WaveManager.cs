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

    void OnDestroy()
    {
        if (grid == null) return;
        grid.Cleared -= OnWaveCleared;
        grid.ReachedBottom -= OnReachedBottom;
    }

    IEnumerator RunWave(int index)
    {
        // Ждём один кадр, чтобы UI успел подписаться на события в своём Start()
        yield return null;

        CurrentWave = index + 1;
        WaveStarted?.Invoke(CurrentWave, TotalWaves);

        yield return new WaitForSeconds(delayBeforeWave);
        if (waves[index].bossPrefab != null)
            SpawnBoss(waves[index].bossPrefab);
        else
            grid.Build(waves[index]);

        if (healPrefab != null && Random.value < healChance)
            healRoutine = StartCoroutine(DropHeal());
    }

    void SpawnBoss(Boss prefab)
    {
        Boss boss = Instantiate(prefab, new Vector3(0f, bossSpawnY, 0f), Quaternion.identity);
        boss.Init(grid.player);
        boss.Died += OnWaveCleared;
        BossSpawned?.Invoke(boss);
    }

    // Хилка падает в случайный момент волны из случайного места сверху
    IEnumerator DropHeal()
    {
        yield return new WaitForSeconds(Random.Range(healDelayMin, healDelayMax));

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
        else
            StartCoroutine(RunWave(CurrentWave));
    }

    void OnReachedBottom()
    {
        GameManager.Instance.GameOver();
    }
}
