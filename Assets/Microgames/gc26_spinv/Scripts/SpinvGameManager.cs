using UnityEngine;

namespace GC26.Spinv
{
    // Состояние игры: очки, жизни, монеты, конец игры, сохранение рекорда
    public class SpinvGameManager : MonoBehaviour
    {
        public const string HighScoreKey = SpinvMicrogame.Id + "_HighScore";
        public const string TotalCoinsKey = SpinvMicrogame.Id + "_TotalCoins";

        public static SpinvGameManager Instance { get; private set; }

        public int startLives = 3;

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Coins { get; private set; }
        public bool IsGameOver { get; private set; }
        // Обучение: смерти нет, рекорды, монеты и достижения не сохраняются
        public bool IsTutorial { get; private set; }

        // Аркада: свой рекорд (в меню рядом с обычным), в таблицу лидеров не пишется
        public bool IsArcade { get; private set; }
        string BestKey => IsArcade ? ArcadeMode.HighScoreKey : HighScoreKey;
        public int HighScore => PlayerPrefs.GetInt(BestKey, 0);

        [Header("Аркада")]
        [Tooltip("Множитель очков: в аркаде растёт с каждым циклом (ставит WaveManager)")]
        [HideInInspector] public float scoreMultiplier = 1f;
        [Tooltip("В аркаде каждые столько очков даётся жизнь (если сердечко потеряно)")]
        public int extraLifeEvery = 10000;
        int nextExtraLife;

        [Header("Достижения")]
        public int highRollerScore = 15000;
        public int piggyBankCoins = 500;
        public const string LifetimeCoinsKey = SpinvMicrogame.Id + "_LifetimeCoins";
        int coinsCollected;
        bool lostLife;
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
            var flow = FindAnyObjectByType<SpinvMicrogame>();
            IsTutorial = flow != null && flow.TutorialRun;
            IsArcade = !IsTutorial && ArcadeMode.IsSelected;
            nextExtraLife = extraLifeEvery;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Игру выключают (выход в меню, рестарт, выход из общей оболочки): время и звук - как были
        void OnDisable()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
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
            Score += Mathf.RoundToInt(amount * scoreMultiplier);
            // Аркада: как в старых автоматах, за каждые extraLifeEvery очков - жизнь
            while (IsArcade && extraLifeEvery > 0 && Score >= nextExtraLife)
            {
                nextExtraLife += extraLifeEvery;
                AddLives(1);
            }
            if (IsArcade && Score >= highRollerScore)
                Achievements.Award(AchievementId.HighRoller);
            StatsChanged?.Invoke();
        }

        public void AddCoins(int amount)
        {
            Coins += amount;
            coinsCollected += amount;
            if (PlayerPrefs.GetInt(LifetimeCoinsKey, 0) + coinsCollected >= piggyBankCoins)
                Achievements.Award(AchievementId.PiggyBank);
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

            lostLife = true;
            Lives = Mathf.Max(IsTutorial ? 1 : 0, Lives - 1);
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
            if (IsGameOver || IsTutorial) return;

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

            if (victory && !IsArcade)
            {
                Achievements.Award(AchievementId.EarthIsSaved);
                if (!lostLife) Achievements.Award(AchievementId.Flawless);
            }
        }

        public bool Won { get; private set; }

        // Монеты копятся между играми, результат попадает в таблицу рекордов. При победе - под именем
        // «???», его заменит имя, введённое на экране победы; при поражении - с последним введённым именем.
        // Один раз за забег
        void SaveProgress()
        {
            if (IsTutorial) return;
            if (progressSaved) return;
            progressSaved = true;

            PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins + Coins);
            PlayerPrefs.SetInt(LifetimeCoinsKey, PlayerPrefs.GetInt(LifetimeCoinsKey, 0) + coinsCollected);
            // Личный рекорд (меню HI-SCORE) отдельно от таблицы, где первое место закреплено за Gekata
            if (Score > HighScore)
                PlayerPrefs.SetInt(BestKey, Score);
            if (!IsArcade) // у аркады таблицы лидеров нет
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
            FindAnyObjectByType<SpinvMicrogame>().Restart();
        }

        public void LoadMenu()
        {
            SaveProgress();
            FindAnyObjectByType<SpinvMicrogame>().ToMenu();
        }
    }
}
