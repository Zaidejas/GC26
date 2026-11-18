using System;
using UnityEngine;

namespace GC26.Towerdefence
{
    public enum LevelState
    {
        /// <summary>Before the game has begun.</summary>
        Waiting,
        /// <summary>Between waves: build towers. The countdown only measures the early-call bonus; the wave never starts by itself.</summary>
        Build,
        /// <summary>A wave is running.</summary>
        Wave,
        Won,
        Lost,
    }

    /// <summary>
    /// Runs one level: Build -> Wave -> Build ... -> Won / Lost.
    /// Lives start at 20; reaching 0 loses, clearing the last wave wins.
    /// Every spawned enemy is passed to RegisterEnemy: kills give gold, enemies at the castle take lives.
    /// The spawner calls FinishSpawning when a wave has no more enemies to send;
    /// the wave is cleared once that happened and no registered enemy is left alive.
    /// </summary>
    public class LevelFlow : MonoBehaviour
    {
        [SerializeField] TowerdefenceGame game;
        [SerializeField] EconomyManager economy;

        [SerializeField, Min(1)] int startLives = 20;
        [SerializeField, Min(1)] int totalWaves = 10;
        [Tooltip("Seconds on the build timer. Calling the wave early pays the seconds left as gold.")]
        [SerializeField, Min(0f)] float buildCountdown = 20f;
        [Tooltip("Wave-clear bonus = base + per wave x wave number (prototype: 20 + 5 x wave).")]
        [SerializeField, Min(0)] int waveClearBase = 20;
        [SerializeField, Min(0)] int waveClearPerWave = 5;
        [Tooltip("Lives left needed for 3 and for 2 stars.")]
        [SerializeField, Min(0)] int threeStarLives = 18;
        [SerializeField, Min(0)] int twoStarLives = 10;

        /// <summary>Raised when bonus gold is paid: a short reason ("Early", "Wave 3 cleared") and the amount.</summary>
        public event Action<string, int> BonusPaid;

        /// <summary>Gold the player gets for starting the next wave right now.</summary>
        public int EarlyCallBonus => State == LevelState.Build ? Mathf.CeilToInt(Countdown) : 0;

        /// <summary>Raised at the start of every attempt (first start and restarts): clear towers, effects, etc.</summary>
        public event Action LevelBegun;
        public event Action<LevelState> StateChanged;
        public event Action<int> LivesChanged;
        /// <summary>Raised with the 1-based wave number. The spawner starts sending enemies on it.</summary>
        public event Action<int> WaveStarted;
        /// <summary>Raised with the 1-based number of the wave just cleared.</summary>
        public event Action<int> WaveCleared;

        public LevelState State { get; private set; } = LevelState.Waiting;
        public int Lives { get; private set; }
        public int StartLives => startLives;
        /// <summary>Last started wave, 1-based; 0 before the first wave.</summary>
        public int CurrentWave { get; private set; }
        public int TotalWaves => totalWaves;
        /// <summary>Seconds left on the build timer (stops at 0).</summary>
        public float Countdown { get; private set; }

        public bool IsOver => State == LevelState.Won || State == LevelState.Lost;

        /// <summary>
        /// Stars for a win (GDD section 3): 3 with 18+ lives left, 2 with 10+, 1 for any win; 0 if not won.
        /// </summary>
        public int Stars => State != LevelState.Won ? 0 : Lives >= threeStarLives ? 3 : Lives >= twoStarLives ? 2 : 1;
        public bool CanStartWave => State == LevelState.Build && CurrentWave < totalWaves;

        int _aliveEnemies;
        bool _spawningFinished;

        void OnEnable()
        {
            if (game == null) return;
            game.Started += BeginLevel;
            if (game.IsStarted && State == LevelState.Waiting) BeginLevel();
        }

        void OnDisable()
        {
            if (game != null) game.Started -= BeginLevel;
        }

        /// <summary>Starts (or restarts) the level from the first build phase.</summary>
        public void BeginLevel()
        {
            Lives = startLives;
            CurrentWave = 0;
            _aliveEnemies = 0;
            _spawningFinished = false;
            if (economy != null) economy.ResetGold();
            LevelBegun?.Invoke();
            LivesChanged?.Invoke(Lives);
            EnterBuild();
        }

        /// <summary>"Start wave" button. Does nothing outside the build phase.</summary>
        public void StartNextWave()
        {
            if (!CanStartWave) return;

            // Early call: the seconds left on the build timer are paid as gold.
            int early = EarlyCallBonus;
            if (early > 0) PayBonus("Early", early);

            CurrentWave++;
            _aliveEnemies = 0;
            _spawningFinished = false;
            SetState(LevelState.Wave);
            Debug.Log($"[{TowerdefenceGame.Id}] wave {CurrentWave}/{totalWaves} started");
            WaveStarted?.Invoke(CurrentWave);
        }

        /// <summary>Call for every enemy the spawner creates during a wave.</summary>
        public void RegisterEnemy(EnemyUnit enemy)
        {
            if (enemy == null || IsOver) return;
            _aliveEnemies++;
            enemy.Died += OnEnemyDied;
            enemy.ReachedCastle += OnEnemyReachedCastle;
        }

        /// <summary>Call when the current wave has no more enemies to send.</summary>
        public void FinishSpawning()
        {
            if (State != LevelState.Wave) return;
            _spawningFinished = true;
            CheckWaveCleared();
        }

        void Update()
        {
            if (State == LevelState.Build && Countdown > 0f)
                Countdown = Mathf.Max(0f, Countdown - Time.deltaTime);
        }

        void OnEnemyDied(EnemyUnit enemy)
        {
            Unsubscribe(enemy);
            if (IsOver) return;
            if (economy != null) economy.Add(enemy.Data.gold);
            _aliveEnemies--;
            CheckWaveCleared();
        }

        void OnEnemyReachedCastle(EnemyUnit enemy)
        {
            Unsubscribe(enemy);
            if (IsOver) return;
            Lives = Mathf.Max(0, Lives - enemy.Data.livesLost);
            LivesChanged?.Invoke(Lives);
            _aliveEnemies--;

            if (Lives <= 0)
            {
                SetState(LevelState.Lost);
                Debug.Log($"[{TowerdefenceGame.Id}] LOST on wave {CurrentWave}");
                return;
            }
            CheckWaveCleared();
        }

        void Unsubscribe(EnemyUnit enemy)
        {
            enemy.Died -= OnEnemyDied;
            enemy.ReachedCastle -= OnEnemyReachedCastle;
        }

        void CheckWaveCleared()
        {
            if (State != LevelState.Wave || !_spawningFinished || _aliveEnemies > 0) return;

            Debug.Log($"[{TowerdefenceGame.Id}] wave {CurrentWave} cleared, lives {Lives}");
            WaveCleared?.Invoke(CurrentWave);
            PayBonus($"Wave {CurrentWave} cleared", waveClearBase + waveClearPerWave * CurrentWave);

            if (CurrentWave >= totalWaves)
            {
                SetState(LevelState.Won);
                Debug.Log($"[{TowerdefenceGame.Id}] WON with {Lives} lives");
            }
            else
            {
                EnterBuild();
            }
        }

        void PayBonus(string reason, int gold)
        {
            if (gold <= 0) return;
            if (economy != null) economy.Add(gold);
            BonusPaid?.Invoke(reason, gold);
        }

        void EnterBuild()
        {
            Countdown = buildCountdown;
            SetState(LevelState.Build);
        }

        void SetState(LevelState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
