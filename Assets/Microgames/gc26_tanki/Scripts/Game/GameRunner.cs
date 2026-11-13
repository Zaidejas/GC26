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

        // ---- Tutorial: one step at a time, each opened by doing the last one ----
        public const int TutorialSteps = 6;
        /// <summary>The current tutorial step from 0, or -1 when no tutorial runs.</summary>
        public int TutorialStep { get; private set; } = -1;
        public bool TutorialFirstWin { get; private set; }
        float tutorialWait;

        public string TutorialText
        {
            get
            {
                switch (TutorialStep)
                {
                    case 0: return "Двигайся: джойстик слева или стрелки / WASD. Проедь пару клеток.";
                    case 1: return "Огонь: круглая кнопка справа или пробел. Разбей ящик перед собой, ему нужно два попадания.";
                    case 2: return "Враг! Подбей его. Здесь враги не стреляют, в бою стреляют.";
                    case 3: return "Из подсвеченных танков выпадают бонусы. Подъедь к бонусу, чтобы взять его.";
                    case 4: return "Подбитые враги заряжают способность танка. Она заряжена: нажми «" + AbilityLabel(Sim.Ability) + "» внизу или E.";
                    case 5: return "Береги реактор внизу: одно попадание в него, и бой проигран. Обучение пройдено!";
                    default: return null;
                }
            }
        }

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
            UpdateTutorial(Time.deltaTime);
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
                TutorialEvent(item.Kind);
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
                        bool boss = item.Enemy == EnemyKind.Boss;
                        world.Smoke(item.X, item.Y, boss ? 10 : 5, boss ? 0.8f : 0.5f);
                        world.Smoulder(item.X, item.Y, boss ? 5f : 2.5f);
                        shake = Mathf.Max(shake, 0.12f);
                        Save.Kills++;
                        if (item.Enemy == EnemyKind.Boss) Save.Bosses++;
                        break;
                    case GameEventKind.PlayerDestroyed:
                        world.Smoke(item.X, item.Y, 7, 0.55f);
                        world.Smoulder(item.X, item.Y, 3f);
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 24, 3.8f);
                        shake = Mathf.Max(shake, 0.3f);
                        Vibrate();
                        break;
                    case GameEventKind.BaseHit:
                        world.Smoke(item.X, item.Y, 5, 0.5f);
                        Sound.BigBlast();
                        world.Burst(item.X, item.Y, WorldView.Hex("6FF2E0"), 20, 3.4f);
                        shake = Mathf.Max(shake, 0.35f);
                        Vibrate();
                        ShowToast("Реактор повреждён: осталось " + Sim.BaseHp);
                        break;
                    case GameEventKind.BaseDestroyed:
                        world.Smoke(item.X, item.Y, 12, 0.9f);
                        world.Smoulder(item.X, item.Y, 8f);
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
                        world.Smoke(item.X, item.Y, 3, 0.4f);
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
                        world.Smoke(item.X, item.Y, 8, 0.7f);
                        world.Smoulder(item.X, item.Y, 2f);
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
                        world.Smoke(item.X, item.Y, 6, 0.6f);
                        world.Smoulder(item.X, item.Y, 4f);
                        Sound.Blast();
                        world.Burst(item.X, item.Y, WorldView.Hex("FF5A3C"), 20, 3.6f);
                        shake = Mathf.Max(shake, 0.2f);
                        Save.Turrets++;
                        break;
                    case GameEventKind.CoreDestroyed:
                        world.Smoke(item.X, item.Y, 14, 1f);
                        world.Smoulder(item.X, item.Y, 10f);
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

        public void StartTutorial()
        {
            Sim.SetDifficulty(ChosenDifficulty);
            Sim.StartTutorial(Save.Stats(), Save.ChosenTank);
            BeginBattle();
            SetTutorialStep(0);
        }

        void SetTutorialStep(int step)
        {
            TutorialStep = step;
            tutorialWait = 0f;
            if (step == 2) Sim.SpawnNow(EnemyKind.Raider);
            if (step == 3)
            {
                // A shield bonus two cells from the player, on a free cell.
                int px = Mathf.FloorToInt(Sim.Player.X), py = Mathf.FloorToInt(Sim.Player.Y);
                int[,] offsets = { { 2, 0 }, { -2, 0 }, { 0, -2 }, { 2, -1 }, { -2, -1 }, { 1, -2 }, { -1, -2 }, { 0, 2 } };
                for (int i = 0; i < offsets.GetLength(0); i++)
                {
                    int x = px + offsets[i, 0], y = py + offsets[i, 1];
                    if (x < 0 || y < 0 || x >= Levels.Width || y >= Levels.Height || Sim.Tiles[x, y] != Tile.Empty) continue;
                    Sim.PlaceBonus(BonusKind.Shield, x, y);
                    break;
                }
            }
            if (step == 4) Sim.FillSuper();
            Sound.Pickup();
        }

        void TutorialEvent(GameEventKind kind)
        {
            if (TutorialStep < 0) return;
            if (TutorialStep == 1 && kind == GameEventKind.CrateBroken) SetTutorialStep(2);
            else if (TutorialStep == 2 && kind == GameEventKind.TankDestroyed) SetTutorialStep(3);
            else if (TutorialStep == 3 && kind == GameEventKind.BonusTaken) SetTutorialStep(4);
            else if (TutorialStep == 4 && (kind == GameEventKind.SuperStrike || kind == GameEventKind.Volley || kind == GameEventKind.Dome || kind == GameEventKind.Dash)) SetTutorialStep(5);
        }

        void UpdateTutorial(float dt)
        {
            if (TutorialStep < 0 || Sim.Mode != GameMode.Tutorial) return;
            tutorialWait += dt;
            Tank p = Sim.Player;
            if (TutorialStep == 0 && p.Alive && Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(7f - 1.5f, 14.5f)) > 1.5f) SetTutorialStep(1);
            // The enemy may get lost; send another one after a while.
            if (TutorialStep == 2 && Sim.Enemies.Count == 0 && tutorialWait > 4f) { Sim.SpawnNow(EnemyKind.Raider); tutorialWait = 0f; }
            if (TutorialStep == 3 && Sim.Bonus == null && tutorialWait > 1f) SetTutorialStep(3);
            if (TutorialStep == 5 && tutorialWait > 3.5f) Sim.FinishTutorial();
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
            if (Sim.Mode == GameMode.Tutorial)
            {
                TutorialStep = -1;
                TutorialFirstWin = !Save.TutorialDone;
                if (TutorialFirstWin)
                {
                    Save.TutorialDone = true;
                    Save.Parts += 10;
                    LastParts += 10;
                }
                Save.Save();
                return;
            }
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
            TutorialStep = -1;
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
            if (Sim.Mode == GameMode.Tutorial) StartTutorial();
            else if (Sim.Mode == GameMode.Survival) StartSurvival();
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
            TutorialStep = -1;
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
