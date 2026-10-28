using UnityEngine;
using UnityEngine.SceneManagement;

// Состояние игры: очки, жизни, монеты, конец игры, сохранение рекорда
public class GameManager : MonoBehaviour
{
    public const string HighScoreKey = "HighScore";
    public const string TotalCoinsKey = "TotalCoins";
    public const string MenuSceneName = "MainMenu";

    public static GameManager Instance { get; private set; }

    public int startLives = 3;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Coins { get; private set; }
    public bool IsGameOver { get; private set; }

    public int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);
    public int TotalCoins => PlayerPrefs.GetInt(TotalCoinsKey, 0);

    // Изменились очки/жизни/монеты - UI обновляется
    public event System.Action StatsChanged;
    // Конец игры: true = победа, false = поражение
    public event System.Action<bool> GameEnded;

    bool progressSaved;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Lives = startLives;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddScore(int amount)
    {
        Score += amount;
        StatsChanged?.Invoke();
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        StatsChanged?.Invoke();
    }

    public void LoseLife()
    {
        if (IsGameOver) return;

        Lives = Mathf.Max(0, Lives - 1);
        StatsChanged?.Invoke();

        if (Lives == 0)
            GameOver();
    }

    public void GameOver() => EndGame(false);
    public void Victory() => EndGame(true);

    void EndGame(bool victory)
    {
        if (IsGameOver) return;

        IsGameOver = true;
        SaveProgress();
        Time.timeScale = 0f;
        GameEnded?.Invoke(victory);
    }

    // Монеты копятся между играми, рекорд обновляется. Сохраняем один раз за забег
    void SaveProgress()
    {
        if (progressSaved) return;
        progressSaved = true;

        PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins + Coins);
        if (Score > HighScore)
            PlayerPrefs.SetInt(HighScoreKey, Score);
        PlayerPrefs.Save();
    }

    public void Restart()
    {
        SaveProgress();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        SaveProgress();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(MenuSceneName);
    }
}
