using System;
using GC26.Tanki.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = GC26.Tanki.Rules.PlayerInput;
using Random = UnityEngine.Random;

namespace GC26.Tanki.Game
{
    /// <summary>The menus the game can show around the battle.</summary>
    public enum Page { Menu, LevelSelect, Workshop, Settings, Game, Assault, Garage, Awards }

    /// <summary>Ties the simulation to the view, sound, input and the save. Created by <see cref="TankiGame"/>.</summary>
    public sealed class GameRunner : MonoBehaviour
    {
        public Difficulty ChosenDifficulty => (Difficulty)Save.Difficulty;

        public GameSim Sim { get; private set; }
        public Controls Controls { get; private set; }
        public SoundBank Sound { get; private set; }
        public SaveData Save { get; private set; }
        public bool OnSplash { get; private set; } = true;
        public WorldView World => world;
        public Page Page { get; private set; } = Page.Menu;
        public bool OnTitle => Page != Page.Game;
        public bool Paused { get; private set; }
        public int BestScore => Save.BestScore;
        public string Toast { get; private set; }
        public float ToastTimer { get; private set; }
        readonly System.Collections.Generic.Queue<string> toasts = new System.Collections.Generic.Queue<string>();

        /// <summary>Parts earned on the level or wave that just ended, for the results screen.</summary>
        public int LastParts { get; private set; }

        WorldView world;
        Camera view;
        float accumulator;
        int lastWidth;
        int lastHeight;
        Vector3 cameraHome;
        float shake;
        GamePhase lastPhase = GamePhase.Playing;
        Page settingsReturn = Page.Menu;

        /// <summary>Called once by the "Exit" button with the best score; set by <see cref="TankiGame"/>.</summary>
        public Action<int> ExitRequested;

        // Global settings this game changes, put back when it leaves the GC26 shell.
        int oldFrameRate;
        int oldSleepTimeout;
        ScreenOrientation oldOrientation;

        void Awake()
        {
            oldFrameRate = Application.targetFrameRate;
            oldSleepTimeout = UnityEngine.Screen.sleepTimeout;
            oldOrientation = UnityEngine.Screen.orientation;
            Application.targetFrameRate = 60;
            UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID || UNITY_IOS
            UnityEngine.Screen.orientation = ScreenOrientation.Portrait;
#endif

            Save = SaveData.Load();
            Sim = new GameSim(System.Environment.TickCount);
            Sim.SetDifficulty(ChosenDifficulty);
            Controls = new Controls();
            Sound = new SoundBank(gameObject) { Muted = !Save.SoundOn };

            view = CameraRig.Create(transform);
            TankiPostFx.Create(transform, view);
            world = new WorldView(transform);
            gameObject.AddComponent<GameHud>().Init(this);
            world.BeginSplash(TankDef.For(Save.ChosenTank).Model);
        }

        void UpdateSplash()
        {
            world.AimSplashCamera(view);
            world.UpdateSplash(Time.deltaTime);
            if (world.SplashJustFired) Sound.Shot(true);
            if (!world.SplashDone) return;

            world.EndSplash();
            OnSplash = false;
            ShowCurrentLevel();
        }

        void Update()
        {
            if (OnSplash)
            {
                UpdateSplash();
                return;
            }

            FitCameraIfScreenChanged();

            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (escape && Page == Page.Game && Sim.Phase == GamePhase.Playing) TogglePause();

            if (OnTitle || Paused)
            {
                Controls.Read();
                world.Sync(Sim, Time.unscaledDeltaTime);
                return;
            }

            PlayerInput input = Controls.Read();
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (accumulator >= GameSim.FixedStep)
            {
                Sim.Tick(GameSim.FixedStep, input);
                accumulator -= GameSim.FixedStep;
            }

            HandleEvents();
            world.Sync(Sim, Time.deltaTime);
            UpdateShake();
            NoticePhaseChange();

            if (ToastTimer > 0f) ToastTimer -= Time.deltaTime;
            else if (toasts.Count > 0) ShowToast(toasts.Dequeue());
        }

        void HandleEvents()
        {
            foreach (GameEvent item in Sim.Events)
            {
                switch (item.Kind)
                {
                    case GameEventKind.Shot:
                        Sound.Shot(item.IsPlayer);
                        break;
                    case GameEventKind.TileChanged:
                        world.RefreshTile(Sim, (int)item.X, (int)item.Y);
                        world.Burst(item.X + 0.5f, item.Y + 0.5f, WorldView.Hex("A9713C"), 5, 2.2f);
                        break;
                    case GameEventKind.BulletSpark:
                        Sound.Spark();
                        world.Burst(item.X, item.Y, WorldView.Hex("FFE9A8"), 3, 1.6f);
                        break;
                    case GameEventKind.TankHit:
                        Sound.Spark();
                        world.Burst(item.X, item.Y, WorldView.Hex("FFB057"), 8, 2.4f);
                        break;
                    case GameEventKind.PlayerHit:
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 10, 2.6f);
                        shake = Mathf.Max(shake, 0.15f);
                        Vibrate();
                        break;
                    case GameEventKind.TankDestroyed:
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF8A3D"), 18, 3.4f);
                        shake = Mathf.Max(shake, 0.12f);
                        Save.Kills++;
                        if (item.Enemy == EnemyKind.Boss) Save.Bosses++;
                        break;
                    case GameEventKind.PlayerDestroyed:
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 24, 3.8f);
                        shake = Mathf.Max(shake, 0.3f);
                        Vibrate();
                        break;
                    case GameEventKind.BaseHit:
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 20, 3.4f);
                        shake = Mathf.Max(shake, 0.35f);
                        Vibrate();
                        ShowToast("Реактор повреждён: осталось " + Sim.BaseHp);
                        break;
                    case GameEventKind.BaseDestroyed:
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 40, 5f);
                        shake = 0.5f;
                        Vibrate();
                        break;
                    case GameEventKind.BonusTaken:
                        Sound.Pickup();
                        world.Burst(item.X, item.Y, WorldView.BonusColor(item.Bonus), 10, 2.4f);
                        ShowToast(GameHud.BonusLine(item.Bonus));
                        break;
                    case GameEventKind.BombBlast:
                        Sound.BigBlast();
                        shake = 0.4f;
                        break;
                    case GameEventKind.SuperReady:
                        Sound.Pickup();
                        ShowToast(AbilityName(Sim.Ability) + " готов!");
                        break;
                    case GameEventKind.SuperStrike:
                        Sound.BigBlast();
                        world.Strike(item.X, item.Y, item.Dir);
                        shake = 0.45f;
                        Vibrate();
                        Save.Abilities++;
                        break;
                    case GameEventKind.Volley:
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FFA040"), 16, 3f);
                        world.Ring(item.X, item.Y, WorldView.Hex("FFA040"), 1.6f, 0.35f);
                        shake = 0.25f;
                        Vibrate();
                        Save.Abilities++;
                        break;
                    case GameEventKind.Dome:
                        Sound.Pickup();
                        world.Ring(item.X, item.Y, WorldView.Hex("9CFF5A"), Tuning.DomeRange, 0.6f);
                        world.Ring(item.X, item.Y, WorldView.Hex("6FF2E0"), Tuning.DomeRange * 0.7f, 0.45f);
                        shake = 0.2f;
                        Vibrate();
                        Save.Abilities++;
                        break;
                    case GameEventKind.Dash:
                        Sound.Shot(true);
                        for (int i = 0; i <= 8; i++)
                            world.Burst(Mathf.Lerp(item.X, item.ToX, i / 8f), Mathf.Lerp(item.Y, item.ToY, i / 8f), WorldView.Hex("C060FF"), 2, 1.2f);
                        Save.Abilities++;
                        break;
                    case GameEventKind.MineBlast:
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF5A3C"), 16, 3.2f);
                        world.Ring(item.X, item.Y, WorldView.Hex("FF5A3C"), 1.2f, 0.4f);
                        shake = Mathf.Max(shake, 0.2f);
                        Vibrate();
                        break;
                    case GameEventKind.MineShot:
                        Sound.Spark();
                        world.Burst(item.X, item.Y, WorldView.Hex("FFB057"), 10, 2.4f);
                        world.Ring(item.X, item.Y, WorldView.Hex("FFB057"), 0.9f, 0.3f);
                        Save.MinesShot++;
                        break;
                    case GameEventKind.Heal:
                        world.Burst(item.X, item.Y, WorldView.Hex("7CE05A"), 4, 1.2f);
                        break;
                    case GameEventKind.RamBlast:
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF7A2A"), 26, 4f);
                        world.Ring(item.X, item.Y, WorldView.Hex("FF7A2A"), Tuning.RamBlast + 0.4f, 0.5f);
                        shake = Mathf.Max(shake, 0.35f);
                        break;
                    case GameEventKind.CrateBroken:
                        Save.Crates++;
                        break;
                    case GameEventKind.TurretHit:
                    case GameEventKind.CoreHit:
                        Sound.Spark();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF5A3C"), 6, 2.2f);
                        break;
                    case GameEventKind.TurretDestroyed:
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF5A3C"), 20, 3.6f);
                        shake = Mathf.Max(shake, 0.2f);
                        Save.Turrets++;
                        break;
                    case GameEventKind.CoreDestroyed:
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF5A3C"), 50, 5.5f);
                        world.Ring(item.X, item.Y, WorldView.Hex("FF5A3C"), 5f, 0.9f);
                        shake = 0.6f;
                        Vibrate();
                        ShowToast("Вражеский реактор уничтожен!");
                        break;
                    case GameEventKind.BossSpawned:
                        Sound.BigBlast();
                        ShowToast("Босс на поле!");
                        shake = 0.3f;
                        break;
                }
            }
            Sim.Events.Clear();
        }

        void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Save.VibrationOn) Handheld.Vibrate();
#endif
        }

        public static string AbilityName(Ability ability)
        {
            switch (ability)
            {
                case Ability.Volley: return "Залп";
                case Ability.Dome: return "Купол";
                case Ability.Dash: return "Рывок";
                default: return "Удар реактора";
            }
        }

        public static string AbilityLabel(Ability ability)
        {
            switch (ability)
            {
                case Ability.Volley: return "ЗАЛП";
                case Ability.Dome: return "КУПОЛ";
                case Ability.Dash: return "РЫВОК";
                default: return "УДАР";
            }
        }

        // Stores parts, records, stars and newly opened levels as soon as a level or wave ends.
        void NoticePhaseChange()
        {
            GamePhase phase = Sim.Phase;
            if (phase == lastPhase) return;
            lastPhase = phase;
            if (phase == GamePhase.Playing) return;

            LastParts = Sim.TakeParts();
            Save.Parts += LastParts;
            if (Sim.Score > Save.BestScore) Save.BestScore = Sim.Score;
            if (Sim.Mode == GameMode.Survival)
            {
                int reached = phase == GamePhase.WaveCleared ? Sim.Wave : Sim.Wave - 1;
                Save.BestWave = Mathf.Max(Save.BestWave, reached);
            }
            bool won = phase == GamePhase.LevelCleared || phase == GamePhase.Victory;
            if (won && Sim.Mode == GameMode.Assault)
            {
                Save.AssaultStars[Sim.LevelIndex] = Mathf.Max(Save.AssaultStars[Sim.LevelIndex], Sim.StarsWon);
                Save.AssaultUnlocked = Mathf.Max(Save.AssaultUnlocked, Mathf.Min(Sim.LevelIndex + 1, Levels.Assault.Length - 1));
            }
            else if (won && Sim.Mode == GameMode.Campaign)
            {
                Save.Stars[Sim.LevelIndex] = Mathf.Max(Save.Stars[Sim.LevelIndex], Sim.StarsWon);
                if (phase == GamePhase.LevelCleared)
                    Save.UnlockedLevel = Mathf.Max(Save.UnlockedLevel, Mathf.Min(Sim.LevelIndex + 1, Levels.All.Length - 1));
            }
            if (won && Sim.LivesLost == 0) Save.Flawless++;
            Save.Save();
            foreach (string line in TankiAwards.Check(Save)) toasts.Enqueue(line);
        }

        void ShowToast(string text)
        {
            Toast = text;
            ToastTimer = 2.2f;
        }

        void UpdateShake()
        {
            view.transform.position = cameraHome + Random.insideUnitSphere * shake * 0.35f;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.2f);
        }

        void FitCameraIfScreenChanged()
        {
            if (UnityEngine.Screen.width == lastWidth && UnityEngine.Screen.height == lastHeight) return;
            lastWidth = UnityEngine.Screen.width;
            lastHeight = UnityEngine.Screen.height;
            CameraRig.Fit(view);
            cameraHome = view.transform.position;
        }

        void ShowCurrentLevel()
        {
            world.BuildLevel(Sim);
            world.Sync(Sim, 0f, true);
            accumulator = 0f;
            lastPhase = Sim.Phase;
        }

        void BeginBattle()
        {
            Page = Page.Game;
            Paused = false;
            ToastTimer = 0f;
            Sim.Events.Clear();
            ShowCurrentLevel();
        }

        // ---- Called by the on-screen menus ----

        public void Open(Page page)
        {
            if (page == Page.Settings && Page != Page.Settings) settingsReturn = Page;
            Page = page;
        }

        public void CloseSettings()
        {
            Page = settingsReturn;
        }

        /// <summary>Starts the campaign on a level, if that level is open.</summary>
        public void StartLevel(int index)
        {
            if (index > Save.UnlockedLevel) return;
            Sim.SetDifficulty(ChosenDifficulty);
            Sim.StartCampaign(index, Save.Stats(), Save.ChosenTank);
            BeginBattle();
        }

        /// <summary>Starts an assault on a fortress, if it is open.</summary>
        public void StartAssault(int index)
        {
            if (index > Save.AssaultUnlocked) return;
            Sim.SetDifficulty(ChosenDifficulty);
            Sim.StartAssault(index, Save.Stats(), Save.ChosenTank);
            BeginBattle();
        }

        public void StartGame()
        {
            StartLevel(Mathf.Min(Save.UnlockedLevel, Levels.All.Length - 1));
        }

        public void StartSurvival()
        {
            Sim.SetDifficulty(ChosenDifficulty);
            Sim.StartSurvival(Save.Stats(), Save.ChosenTank);
            BeginBattle();
        }

        public void Retry()
        {
            if (Sim.Mode == GameMode.Survival) StartSurvival();
            else if (Sim.Mode == GameMode.Assault) StartAssault(Sim.LevelIndex);
            else StartLevel(Sim.LevelIndex);
        }

        public void ChooseUpgrade(RunUpgrade upgrade)
        {
            Sim.ChooseUpgrade(upgrade);
            lastPhase = Sim.Phase;
        }

        public bool Buy(Stat stat)
        {
            bool bought = Save.Buy(stat);
            if (bought) Sound.Pickup();
            return bought;
        }

        public void BuyTank(PlayerTank tank)
        {
            if (!Save.BuyTank(tank)) return;
            Sound.Pickup();
            foreach (string line in TankiAwards.Check(Save)) toasts.Enqueue(line);
        }

        public void ChooseTank(PlayerTank tank)
        {
            Save.ChooseTank(tank);
        }

        public void ChooseDifficulty(Difficulty difficulty)
        {
            Save.Difficulty = (int)difficulty;
            Save.Save();
        }

        public void SetSound(bool on)
        {
            Save.SoundOn = on;
            Sound.Muted = !on;
            Save.Save();
        }

        public void SetVibration(bool on)
        {
            Save.VibrationOn = on;
            Save.Save();
            Vibrate();
        }

        public void ToMenu()
        {
            Page = Page.Menu;
            Paused = false;
            Sim.SetDifficulty(ChosenDifficulty);
            Sim.StartCampaign(Mathf.Min(Save.UnlockedLevel, Levels.All.Length - 1), Save.Stats(), Save.ChosenTank);
            Sim.Events.Clear();
            ShowCurrentLevel();
        }

        public void ContinueToNextLevel()
        {
            Sim.NextLevel();
            ShowCurrentLevel();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            if (Paused) Save.Save();
        }

        /// <summary>Leaves the game for the GC26 menu.</summary>
        public void Quit()
        {
            Save.Save();
            Action<int> exit = ExitRequested;
            ExitRequested = null;
            exit?.Invoke(Save.BestScore);
        }

        void OnDestroy()
        {
            Save?.Save();
            Application.targetFrameRate = oldFrameRate;
            UnityEngine.Screen.sleepTimeout = oldSleepTimeout;
#if UNITY_ANDROID || UNITY_IOS
            UnityEngine.Screen.orientation = oldOrientation;
#endif
            // Materials, textures and meshes made in code are not part of the scene, so free them here.
            Resources.UnloadUnusedAssets();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Page == Page.Game && Sim.Phase == GamePhase.Playing) Paused = true;
            Save.Save();
        }
    }
}
