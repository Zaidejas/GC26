using System.Collections;
using UnityEngine;

// Запускает волны по очереди. После последней - победа
public class WaveManager : MonoBehaviour
{
    public WaveData[] waves;
    public InvaderGrid grid;
    [Tooltip("Пауза с надписью «WAVE N» перед появлением пришельцев")]
    public float delayBeforeWave = 2f;

    public int CurrentWave { get; private set; }
    public int TotalWaves => waves.Length;

    // (номер волны, всего волн)
    public event System.Action<int, int> WaveStarted;

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
        grid.Build(waves[index]);
    }

    void OnWaveCleared()
    {
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
