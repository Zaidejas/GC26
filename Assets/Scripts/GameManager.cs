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

    [Header("Бонусы при победе")]
    public int bonusPerLife = 1000;
    public int bonusPerCoin = 10;

    // Итоги забега для экранов конца игры
    public int BaseScore { get; private set; }
    public int LivesBonus { get; private set; }
    public int CoinsBonus { get; private set; }
    public int PreviousBest { get; private set; }
    // Место в таблице рекордов (0..4) или -1
    public int TableIndex { get; private set; } = -1;

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

#if UNITY_EDITOR
    // Только в редакторе: F9 - победа, F10 - поражение, чтобы смотреть экраны конца игры
    void Update()
    {
        if (IsGameOver) return;
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f9Key.wasPressedThisFrame) Victory();
        else if (keyboard.f10Key.wasPressedThisFrame) GameOver();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F9)) Victory();
        else if (Input.GetKeyDown(KeyCode.F10)) GameOver();
#endif
    }
#endif

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

    public void SpendCoins(int amount)
    {
        Coins -= amount;
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

    // Жизней не может стать больше, чем было на старте
    public void AddLives(int amount)
    {
        if (IsGameOver) return;

        Lives = Mathf.Min(startLives, Lives + amount);
        StatsChanged?.Invoke();
    }

    public void GameOver() => EndGame(false);
    public void Victory() => EndGame(true);

    void EndGame(bool victory)
    {
        if (IsGameOver) return;

        IsGameOver = true;
        Won = victory;

        // При победе к счёту добавляются бонусы за оставшиеся жизни и монеты забега
        BaseScore = Score;
        if (victory)
        {
            LivesBonus = Lives * bonusPerLife;
            CoinsBonus = Coins * bonusPerCoin;
            Score += LivesBonus + CoinsBonus;
        }

        HighScoreTable.Load(); // применяет сброс таблицы, если он новый, до чтения рекорда
        PreviousBest = HighScore; // личный рекорд игрока до этого забега
        SaveProgress();
        Time.timeScale = 0f;
        GameEnded?.Invoke(victory);
    }

    public bool Won { get; private set; }

    // Монеты копятся между играми, результат попадает в таблицу рекордов. При победе - под именем
    // «???», его заменит имя, введённое на экране победы; при поражении - с последним введённым именем.
    // Один раз за забег
    void SaveProgress()
    {
        if (progressSaved) return;
        progressSaved = true;

        PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins + Coins);
        // Личный рекорд (меню HI-SCORE) отдельно от таблицы, где первое место закреплено за Gekata
        if (Score > HighScore)
            PlayerPrefs.SetInt(HighScoreKey, Score);
        TableIndex = HighScoreTable.Insert(Won ? HighScoreTable.UnnamedPlayer : HighScoreTable.LastInitials, Score);
        PlayerPrefs.Save();
    }

    public void RenameRecord(string initials)
    {
        HighScoreTable.LastInitials = initials;
        HighScoreTable.Rename(TableIndex, initials);
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
