using System;
using System.Collections;
using System.Collections.Generic;
using GC26.Core;
using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Tetris
{
    /// <summary>
    /// Entry point and the game's IMicrogame: owns the game, reads the keyboard and draws the HUD.
    /// Runs on its own when the scene is opened without the GC26 shell.
    /// </summary>
    public sealed partial class TetrisGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_tetris";
        static readonly string RecordsPrefix = MicrogameSave.Key(Id, "");
        static readonly string TestRecordsPrefix = MicrogameSave.Key(Id, "Test_");
        const string BestScoreName = "BestScore";
        const string MarathonTableName = "MarathonTable";
        const string BattleTableName = "BattleTable";
        const string CoinsName = "Coins";
        const float RepeatDelay = 0.17f;
        const float RepeatInterval = 0.045f;
        const float PopupLife = 1.1f;
        const float BannerLife = 1.5f;

        [SerializeField] Material spriteMaterial;
        [SerializeField] Camera gameCamera;

        Action<int> exitToShell;
        int sessionBest;

        TetrisMatch game;
        BoardView view;
        GameAudio gameAudio;
        Camera mainCamera;
        int bestScore;
        string recordsPrefix = RecordsPrefix;
        Leaderboard marathonTable;
        Leaderboard battleTable;
        int lastPlace;
        int heldDirection;
        float repeatTimer;
        float shownScore;
        float scoreKick;
        float comboKick;
        bool hardDropping;

        // A new popup replaces the previous one in its slot, so fast clears never stack text on text.
        enum PopupSlot { Points, Lines, Combo, Level, Special, Damage, Defeat, Attack }

        sealed class Popup
        {
            public PopupSlot Slot;
            public string Text;
            public float X, Y, Size, Life, MaxLife, Rise;
            public Color Color;
        }

        readonly List<Popup> popups = new List<Popup>();

        // Main menu. Items are laid out in world units so they scale with the window, like the rest of the HUD.
        enum MenuItem { Marathon, Battle, Shop, Achievements, Sound, Exit }

        static readonly MenuItem[] MenuItems = { MenuItem.Marathon, MenuItem.Battle, MenuItem.Shop, MenuItem.Achievements, MenuItem.Sound, MenuItem.Exit };

        static float MenuTopY { get { return HudLayout.MenuTopY; } }
        const float MenuStepY = 1.7f;
        const float MenuItemWidth = 12f;
        const float MenuItemHeight = 1.45f;

        GameMode mode = GameMode.Marathon;
        bool inMenu = true;
        int menuIndex;
        Vector2 lastMousePosition;

        // Developer switches, read from the command line (see ReadCommandLine).
        bool autoplay;
        float autoplayTimer;
        System.Random autoplayRandom;
        bool noRecords;
        bool assist;
        bool assistPending;
        bool specialCycle;
        bool specialCyclePending;
        int specialCycleIndex;
        bool logSlowFrames;

        void Awake()
        {
            // Layout is static, so read the window afresh rather than trust a previous visit.
            HudLayout.Refresh(Screen.width, Screen.height);
            mainCamera = gameCamera != null ? gameCamera : Camera.main;
            view = new BoardView(transform, spriteMaterial);
            ReadCommandLine();
            LegacyRecords.Move();
            LoadLanguage();
            LoadRecords();
            gameAudio = new GameAudio(gameObject, autoplay);
            CreateBattleView();
            StartNewGame();
            if (autoplay || assist || specialCycle)
                inMenu = false;
        }

        void OnDestroy()
        {
            // The shell unloads the scene; code-made textures, sprites and clips go with it.
            RuntimeAssets.ReleaseAll();
            SaveAchievements();
        }

        // --- GC26 shell ------------------------------------------------------------------------

        /// <summary>Called by the GC26 shell after it loads the scene. The game opens on its own menu.</summary>
        public void Begin(Action<int> exit)
        {
            exitToShell = exit;
            // The shell has its own listener; two at once make Unity complain.
            AudioListener own = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
            if (own != null && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length > 1)
                own.enabled = false;
            Debug.Log("[BlockDrop] Begin from the GC26 shell");
        }

        /// <summary>Leaves to the GC26 shell with the best score of this visit. Records are already saved.</summary>
        public void ExitToMenu(int score)
        {
            Action<int> exit = exitToShell;
            exitToShell = null;
            exit?.Invoke(score);
        }

        // --- records --------------------------------------------------------------------------

        void LoadRecords()
        {
            bestScore = PlayerPrefs.GetInt(recordsPrefix + BestScoreName, 0);
            coinsTotal = PlayerPrefs.GetInt(recordsPrefix + CoinsName, 0);
            marathonTable = Leaderboard.Parse(PlayerPrefs.GetString(recordsPrefix + MarathonTableName, ""), false);
            battleTable = Leaderboard.Parse(PlayerPrefs.GetString(recordsPrefix + BattleTableName, ""), false);

            LoadShop();
            LoadAchievements();

            // A best score from before the table existed becomes its first entry.
            if (bestScore > 0 && marathonTable.Entries.Count == 0)
                marathonTable.Add(bestScore);
        }

        void SaveMarathonResult()
        {
            if (autoplay || noRecords)
                return;
            lastPlace = marathonTable.Add(game.Score);
            PlayerPrefs.SetString(recordsPrefix + MarathonTableName, marathonTable.Serialize());
            if (game.Score > bestScore)
            {
                bestScore = game.Score;
                PlayerPrefs.SetInt(recordsPrefix + BestScoreName, bestScore);
            }
            PlayerPrefs.Save();
            Debug.Log("[BlockDrop] Marathon result " + game.Score + " place=" + lastPlace + " table=" + marathonTable.Serialize());
        }

        void SaveBattleResult()
        {
            if (autoplay || noRecords)
                return;
            lastPlace = battleTable.Add(game.Score);
            PlayerPrefs.SetString(recordsPrefix + BattleTableName, battleTable.Serialize());
            PlayerPrefs.Save();
            Debug.Log("[BlockDrop] Battle result " + game.Score + " place=" + lastPlace + " table=" + battleTable.Serialize());
        }

        static int Best(Leaderboard table)
        {
            return table.Entries.Count > 0 ? (int)table.Entries[0] : 0;
        }

        string PlaceText()
        {
            if (lastPlace == 1)
                return T("Новый рекорд!\n", "New record!\n");
            return lastPlace > 1 ? T("Место " + lastPlace + " в таблице\n", "Place " + lastPlace + " in the table\n") : "";
        }

        // --- main menu ------------------------------------------------------------------------

        void UpdateMenu()
        {
            if (Pressed(Key.M))
                gameAudio.ToggleMute();
            HandleLanguageButton();

            if (HandleButtons(ref menuIndex, MenuItems.Length, MenuTopY, MenuStepY, MenuItemWidth, MenuItemHeight))
                ActivateMenuItem();
        }

        static float MenuItemY(int index)
        {
            return MenuTopY - index * MenuStepY;
        }

        void ActivateMenuItem()
        {
            switch (MenuItems[menuIndex])
            {
                case MenuItem.Marathon:
                case MenuItem.Battle:
                    mode = MenuItems[menuIndex] == MenuItem.Battle ? GameMode.Battle : GameMode.Marathon;
                    Debug.Log("[BlockDrop] Menu: play " + mode);
                    // Out of the menu first: a battle that really starts uses up the bought boosters.
                    inMenu = false;
                    StartNewGame();
                    gameAudio.Play("hold");
                    break;
                case MenuItem.Shop:
                    OpenShop();
                    break;
                case MenuItem.Achievements:
                    OpenAchievements();
                    break;
                case MenuItem.Sound:
                    gameAudio.ToggleMute();
                    Debug.Log("[BlockDrop] Menu: sound muted=" + gameAudio.Muted);
                    gameAudio.Play("hold");
                    break;
                case MenuItem.Exit:
                    Debug.Log("[BlockDrop] Menu: exit");
                    if (exitToShell != null)
                        ExitToMenu(sessionBest);
                    else
                        Application.Quit();
                    break;
            }
        }

        void BackToMenu()
        {
            Debug.Log("[BlockDrop] Back to menu");
            SaveAchievements();
            // In the menu first, so the background game never takes the boosters.
            inMenu = true;
            StartNewGame();
            menuIndex = 0;
        }

        string MenuText(MenuItem item)
        {
            switch (item)
            {
                case MenuItem.Marathon: return T("Марафон", "Marathon");
                case MenuItem.Battle: return T("Бой с ботом", "Bot battle");
                case MenuItem.Shop: return T("Магазин", "Shop");
                case MenuItem.Achievements: return T("Достижения", "Achievements") + "  " + achievements.UnlockedCount + "/" + TetrisAchievements.All.Length;
                case MenuItem.Sound: return T("Звук: ", "Sound: ") + OnOff(!gameAudio.Muted);
                default: return exitToShell != null ? T("В меню GC26", "Back to GC26") : T("Выход", "Exit");
            }
        }

        void FillWorld(float centerX, float centerY, float width, float height, Color color)
        {
            Vector2 min = WorldToGui(centerX - width / 2f, centerY - height / 2f);
            Vector2 max = WorldToGui(centerX + width / 2f, centerY + height / 2f);
            GUI.color = color;
            GUI.DrawTexture(new Rect(min.x, max.y, max.x - min.x, min.y - max.y), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // --- a game ---------------------------------------------------------------------------

        void StartNewGame()
        {
            game = new TetrisMatch(System.Environment.TickCount, true, mode);
            game.RowsCleared += view.TriggerFlash;
            game.RowsCleared += rows => Debug.Log("[BlockDrop] Rows cleared: " + rows.Count + ", score " + game.Score);
            game.PieceLocked += OnPieceLocked;
            game.LinesCleared += OnLinesCleared;
            game.HardDropped += OnHardDropped;
            game.LevelChanged += OnLevelChanged;
            game.SpecialTriggered += OnSpecialTriggered;
            game.GameEnded += OnGameEnded;
            heldDirection = 0;
            SyncPointer();
            ResetGameOverButtons();
            lastPlace = 0;
            shownScore = 0f;
            scoreKick = 0f;
            comboKick = 0f;
            popups.Clear();
            view.ResetForNewGame();
            ResetBattle();
            assistPending = assist;
            specialCyclePending = specialCycle;
        }

        void OnSpecialTriggered(SpecialResult result)
        {
            Debug.Log("[BlockDrop] Special " + result.Kind + " at " + result.X + "," + result.Y +
                      " removed=" + result.Removed.Count + " points=" + result.Points + " score=" + game.Score);
            view.BurstRemoved(result);
            Track(result.Kind == SpecialKind.Bomb ? AchievementStat.Bombs : AchievementStat.Lightnings, 1);
            Track(AchievementStat.Score, game.Score);
            Vector2 center = BoardView.GridPosition(result.X, result.Y);
            Color tint = SpecialIcons.ColorOf(result.Kind);
            string points = result.Points > 0 ? "  +" + result.Points.ToString("N0") : "";
            switch (result.Kind)
            {
                case SpecialKind.Bomb:
                    gameAudio.Play("bomb");
                    view.Effects.Shake(1f);
                    view.Effects.Ring(center, new Color(tint.r, tint.g, tint.b, 0.6f), 9f, 0.35f);
                    AddPopup(PopupSlot.Special, T("БОМБА!", "BOMB!") + points, -0.4f, 1.15f, BannerLife, 0.6f, tint);
                    break;
                case SpecialKind.Lightning:
                    gameAudio.Play("zap");
                    view.Effects.Shake(0.55f);
                    view.Effects.RowBeam(0f, center.y, Board.Width, new Color(tint.r, tint.g, tint.b, 0.9f));
                    AddPopup(PopupSlot.Special, T("МОЛНИЯ!", "LIGHTNING!") + points, -0.4f, 1.15f, BannerLife, 0.6f, tint);
                    break;
            }
        }

        void OnPieceLocked(PieceType type)
        {
            Debug.Log("[BlockDrop] Locked " + type + ", score " + game.Score);
            view.OnPieceLocked(game);
            // A hard drop already played its own, heavier landing sound.
            if (!hardDropping)
                gameAudio.Play("lock", 0.7f);
            hardDropping = false;
            assistPending = assist;
            specialCyclePending = specialCycle;
        }

        void OnHardDropped(int fromY, int toY)
        {
            hardDropping = true;
            view.HardDropTrail(game, fromY, toY);
            view.LandingDust(game, toY);
            view.Effects.Shake(0.28f + Mathf.Min(fromY - toY, 20) * 0.012f);
            gameAudio.Play("harddrop");
        }

        void OnLinesCleared(LineClear clear)
        {
            int count = clear.Rows.Count;
            Track(AchievementStat.LinesTotal, count);
            Track(AchievementStat.LinesAtOnce, count);
            Track(AchievementStat.Combo, clear.Combo);
            Debug.Log("[BlockDrop] Clear rows=" + count + " points=" + clear.Points + " combo=" + clear.Combo +
                      " multiplier=" + clear.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      " score=" + game.Score + " level=" + game.Level);

            view.BurstRows(clear.Rows);
            view.Effects.Shake(count >= 4 ? 0.95f : 0.3f + count * 0.1f);

            // Each step of a combo plays a little higher.
            float pitch = Mathf.Pow(2f, Mathf.Min(clear.Combo - 1, 8) / 12f);
            gameAudio.Play("clear" + Mathf.Clamp(count, 1, 4), 1f, pitch);

            float rowY = 0f;
            foreach (int row in clear.Rows)
                rowY += row;
            float worldY = BoardView.BoardBottom + rowY / count + 0.5f;

            scoreKick = 1f;
            if (battle != null)
                ApplyBattleHit(clear);
            AddPopup(PopupSlot.Points, "+" + clear.Points.ToString("N0"), worldY, 1.25f + count * 0.14f, PopupLife, 2.6f, Color.white);

            if (count >= 4)
            {
                AddPopup(PopupSlot.Lines, T("ЧЕТЫРЕ ЛИНИИ!", "FOUR LINES!"), 4.5f, 1.3f, BannerLife, 0.8f, view.AccentColor);
                Color ring = view.AccentColor;
                ring.a = 0.45f;
                view.Effects.Ring(new Vector2(0f, worldY), ring, 20f, 0.5f);
            }
            else if (count == 3)
            {
                AddPopup(PopupSlot.Lines, T("ТРИ ЛИНИИ!", "THREE LINES!"), 4.5f, 1.2f, BannerLife, 0.8f, view.AccentColor);
            }

            if (clear.Combo >= 2)
            {
                comboKick = 1f;
                Color heat = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.25f), (clear.Multiplier - 1.5f) / 2.5f);
                AddPopup(PopupSlot.Combo, T("КОМБО ×", "COMBO ×") + MultiplierText(clear.Multiplier), 2.3f, 1.2f, BannerLife, 0.6f, heat);
                Color ring = heat;
                ring.a = 0.4f;
                view.Effects.Ring(new Vector2(0f, worldY), ring, 8f + Mathf.Min(clear.Combo, 7) * 1.2f, 0.35f);
            }
        }

        void OnLevelChanged(int level)
        {
            Debug.Log("[BlockDrop] Level " + level);
            Track(AchievementStat.Level, level);
            view.SetLevel(level, false);
            gameAudio.Play("levelup");
            AddPopup(PopupSlot.Level, T("УРОВЕНЬ ", "LEVEL ") + level, 6.6f, 1.6f, BannerLife + 0.4f, 0.5f, Color.white);
            view.Effects.Ring(Vector2.zero, new Color(1f, 1f, 1f, 0.3f), 26f, 0.6f);
        }

        void OnGameEnded()
        {
            Debug.Log("[BlockDrop] Game over, score " + game.Score);
            Track(AchievementStat.Score, game.Score);
            SaveAchievements();
            sessionBest = Mathf.Max(sessionBest, game.Score);
            gameAudio.Play("gameover");
            view.Effects.Shake(0.8f);
            // Each mode keeps its own table: a battle scores extra for the bot, so the two do not compare.
            if (game.Mode == GameMode.Marathon)
                SaveMarathonResult();
            else
                SaveBattleResult();
        }

        static string MultiplierText(float multiplier)
        {
            return multiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        void AddPopup(PopupSlot slot, string text, float y, float size, float life, float rise, Color color, float x = 0f)
        {
            popups.RemoveAll(popup => popup.Slot == slot);
            popups.Add(new Popup { Slot = slot, Text = text, X = x, Y = y, Size = size, Life = life, MaxLife = life, Rise = rise, Color = color });
        }

        void UpdateJuice(float deltaTime)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                popup.Life -= deltaTime;
                popup.Y += popup.Rise * deltaTime;
                if (popup.Life <= 0f)
                    popups.RemoveAt(i);
            }
            shownScore = Mathf.MoveTowards(shownScore, game.Score, Mathf.Max(60f, (game.Score - shownScore) * 8f) * deltaTime);
            scoreKick = Mathf.Max(0f, scoreKick - deltaTime * 4f);
            comboKick = Mathf.Max(0f, comboKick - deltaTime * 3f);
            UpdateBattleLooks(deltaTime);

            GameStateForAudio music = inMenu ? GameStateForAudio.Paused
                : game.State == GameState.Playing ? GameStateForAudio.Playing
                : game.State == GameState.Paused ? GameStateForAudio.Paused : GameStateForAudio.Stopped;
            gameAudio.UpdateMusic(music, game.Level, BoardView.Tension(game), deltaTime);
        }

        void Update()
        {
            FitCameraToWindow();
            if (logSlowFrames && Time.unscaledDeltaTime > 0.1f)
                Debug.Log("[BlockDrop] Slow frame " + Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) + " ms at " + Time.realtimeSinceStartup.ToString("F2") + " menu=" + inMenu);
            if (inMenu)
            {
                if (inShop)
                    UpdateShop();
                else if (inAchievements)
                    UpdateAchievementsScreen();
                else
                    UpdateMenu();
                UpdateAchievementToasts();
                UpdateMenuLooks(Time.deltaTime);
                view.Render(game, Time.deltaTime);
                UpdateJuice(Time.deltaTime);
                mainCamera.backgroundColor = view.BackgroundColor;
                // The menu is laid out around the middle of the screen in both layouts.
                mainCamera.transform.position = new Vector3(0f, 0f, -10f);
                return;
            }

            pauseInputUsed = false;
            HandleSystemKeys();
            if (inMenu)
                return;
            ApplyAssist();
            ApplySpecialCycle();
            // A key that pressed a pause button must not also act on the piece.
            if (!pauseInputUsed)
            {
                HandleMovement();
                HandleMouse();
            }
            if (game.IsFinished)
                UpdateGameOverButtons();
            if (autoplay)
                RunAutoplay();

            bool softDrop = Held(Key.DownArrow) || Held(Key.S);
            game.Tick(Time.deltaTime, softDrop);
            UpdateBotAttacks(Time.deltaTime);
            UpdateAchievementToasts();
            view.Render(game, Time.deltaTime);
            UpdateJuice(Time.deltaTime);
            mainCamera.backgroundColor = view.BackgroundColor;
            Vector2 shake = view.Effects.ShakeOffset;
            mainCamera.transform.position = new Vector3(shake.x, HudLayout.GameCameraY + shake.y, -10f);
        }

        // Test aid (-specialCycle): every piece is special, bomb and lightning in turn.
        void ApplySpecialCycle()
        {
            if (!specialCyclePending || game.State != GameState.Playing)
                return;
            specialCyclePending = false;
            game.ForceCurrentSpecial((SpecialKind)(1 + specialCycleIndex % 2));
            specialCycleIndex++;
        }

        // Test aid (-assist): prepares the bottom rows so that hard-dropping the current piece clears them.
        void ApplyAssist()
        {
            if (!assistPending || game.State != GameState.Playing)
                return;
            assistPending = false;
            if (game.CurrentType == PieceType.I)
                game.Rotate(1);
            game.Board.Clear();

            // A piece can only pass through a filled row whose gap covers every part of it below that row.
            Cell[] shape = PieceShapes.Get(game.CurrentType, game.CurrentRotation);
            int landing = game.GhostY;
            var below = new HashSet<int>();
            for (int relativeY = 0; relativeY < PieceShapes.BoxSize(game.CurrentType); relativeY++)
            {
                var mask = new HashSet<int>();
                foreach (Cell c in shape)
                {
                    if (c.Y == relativeY)
                        mask.Add(c.X);
                }
                if (mask.Count == 0)
                    continue;
                if (!below.IsSubsetOf(mask))
                    break;
                below.UnionWith(mask);
                int row = landing + relativeY;
                for (int x = 0; x < Board.Width; x++)
                    game.Board.Set(x, row, mask.Contains(x - game.CurrentX) ? Board.Empty : (x + row + 2) % 7);
            }
        }

        // IMGUI text colours skip the linear-to-sRGB step, so in a linear project they would come out too dark.
        static Color GuiColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;
        }

        static bool Pressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        static bool Held(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }

        void FitCameraToWindow()
        {
            if (HudLayout.Refresh(Screen.width, Screen.height) || !layoutApplied)
            {
                layoutApplied = true;
                view.ApplyLayout();
                RelayoutBattle();
                Debug.Log("[BlockDrop] Layout " + (HudLayout.Portrait ? "portrait" : "landscape") + " " + Screen.width + "x" + Screen.height);
            }
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            mainCamera.orthographicSize = HudLayout.OrthoSize(aspect);
        }

        bool layoutApplied;

        void HandleSystemKeys()
        {
            bool wasPaused = game.State == GameState.Paused;
            if (Pressed(Key.P) || Pressed(Key.Escape))
            {
                game.TogglePause();
                if (game.State == GameState.Paused)
                    OpenPauseMenu();
            }
            if (Pressed(Key.M))
                gameAudio.ToggleMute();
            if (Pressed(Key.L))
                SetLanguage(!english);
            // Enter after a game is the game-over screen's "new game" button.
            if (Pressed(Key.R))
                StartNewGame();
            // Not while playing, so a stray key can't throw a game away.
            if (Pressed(Key.Backspace) && game.State != GameState.Playing)
                BackToMenu();
            // Not on the frame the pause opened: that key press was for opening it.
            if (wasPaused && !inMenu && game.State == GameState.Paused)
                UpdatePauseMenu();
        }

        void HandleMovement()
        {
            if (game.State != GameState.Playing)
                return;

            if (Pressed(Key.UpArrow) || Pressed(Key.W) || Pressed(Key.X))
            {
                if (game.Rotate(1))
                    gameAudio.Play("rotate", 0.55f);
            }
            if (Pressed(Key.Z))
            {
                if (game.Rotate(-1))
                    gameAudio.Play("rotate", 0.55f, 0.9f);
            }
            if (Pressed(Key.C) || Pressed(Key.LeftShift) || Pressed(Key.RightShift))
            {
                if (game.Hold())
                    gameAudio.Play("hold", 0.7f);
            }

            HandleHorizontalRepeat();

            if (Pressed(Key.Space))
                game.HardDrop();
        }

        void HandleHorizontalRepeat()
        {
            bool left = Held(Key.LeftArrow) || Held(Key.A);
            bool right = Held(Key.RightArrow) || Held(Key.D);
            int direction = left == right ? 0 : (left ? -1 : 1);

            if (direction != heldDirection)
            {
                heldDirection = direction;
                repeatTimer = RepeatDelay;
                if (direction != 0 && game.Move(direction))
                    gameAudio.Play("move", 0.5f);
                return;
            }

            if (direction == 0)
                return;

            repeatTimer -= Time.deltaTime;
            while (repeatTimer <= 0f)
            {
                if (game.Move(direction))
                    gameAudio.Play("move", 0.35f);
                repeatTimer += RepeatInterval;
            }
        }

        void OnGUI()
        {
            float unit = Screen.height / (mainCamera.orthographicSize * 2f);

            if (inMenu)
            {
                if (inShop)
                    DrawShop(unit);
                else if (inAchievements)
                    DrawAchievementsScreen(unit);
                else
                    DrawMenu(unit);
                DrawAchievementToast(unit);
                return;
            }

            bool portrait = HudLayout.Portrait;
            Vector2 holdLabel = HudLayout.HoldLabel, nextLabel = HudLayout.NextLabel;
            float headerSize = portrait ? 0.45f : 0.55f;
            Label(T("ЗАПАС", "HOLD"), holdLabel.x, holdLabel.y, unit, headerSize, TextAnchor.MiddleCenter, Muted);
            Label(T("ДАЛЕЕ", "NEXT"), nextLabel.x, nextLabel.y, unit, headerSize, TextAnchor.MiddleCenter, Muted);

            Stat(T("СЧЁТ", "SCORE"), Mathf.RoundToInt(shownScore).ToString("N0"), StatSlot.Score, unit, 1f + scoreKick * 0.35f);
            Stat(T("УРОВЕНЬ", "LEVEL"), game.Level.ToString("N0"), StatSlot.Level, unit);
            Stat(T("ЛИНИИ", "LINES"), game.Lines.ToString("N0"), StatSlot.Lines, unit);
            if (game.Mode == GameMode.Battle)
            {
                // In the tall layout the bot panel shows coins and the best score itself.
                if (!portrait)
                {
                    int best = Mathf.Max(Best(battleTable), game.Score);
                    Label(T("рекорд ", "best ") + best.ToString("N0"), BoardView.LeftPanelX, 1.2f, unit, 0.36f, TextAnchor.MiddleCenter, Muted);
                    Stat(T("МОНЕТЫ", "COINS"), Mathf.RoundToInt(shownCoins).ToString("N0"), StatSlot.Second, unit, 1f + coinKick * 0.25f);
                }
            }
            else
            {
                Stat(T("РЕКОРД", "BEST"), Mathf.Max(bestScore, game.Score).ToString("N0"), StatSlot.Second, unit);
            }

            // Keyboard hints and the side title only fit the wide layout.
            if (!portrait)
            {
                Label("BlockDrop", BoardView.LeftPanelX, 10.2f, unit, 0.8f, TextAnchor.MiddleCenter, view.AccentColor);
                Label(ControlsHint(), 0f, -10.55f, unit, 0.42f, TextAnchor.MiddleCenter, Muted);
            }
            if (game.Mode == GameMode.Battle)
            {
                DrawBotPanel(unit);
                DrawAttackWarning(unit);
            }
            else if (!portrait)
            {
                Label(T("C запас   P пауза   R заново", "C hold   P pause   R restart"), BoardView.RightPanelX, -3.0f, unit, 0.4f, TextAnchor.MiddleCenter, Muted);
                Label(T("M звук: ", "M sound: ") + OnOff(!gameAudio.Muted), BoardView.RightPanelX, -3.7f, unit, 0.4f, TextAnchor.MiddleCenter, Muted);
            }

            if (game.Combo >= 1 && !game.IsFinished)
            {
                // The multiplier the next clear would earn, so keeping the chain feels worth it.
                float next = TetrisMatch.ComboMultiplier(game.Combo + 1);
                Vector2 combo = HudLayout.ComboLine;
                if (portrait)
                {
                    Label(T("КОМБО ", "COMBO ") + game.Combo + T("   ·   дальше ×", "   ·   next ×") + MultiplierText(next), combo.x, combo.y, unit, 0.5f * (1f + comboKick * 0.3f), TextAnchor.MiddleCenter, view.AccentColor);
                }
                else
                {
                    Label(T("КОМБО ", "COMBO ") + game.Combo, combo.x, combo.y, unit, 0.6f, TextAnchor.MiddleCenter, view.AccentColor);
                    Label(T("дальше ×", "next ×") + MultiplierText(next), combo.x, combo.y - 0.9f, unit, 0.62f * (1f + comboKick * 0.4f), TextAnchor.MiddleCenter, Bright);
                }
            }

            if (game.Mode != GameMode.Battle)
            {
                float legendX = HudLayout.LegendTextX;
                Label(T("взрыв 5×5", "blast 5×5"), legendX, HudLayout.LegendIcon(0).y, unit, 0.4f, TextAnchor.MiddleCenter, Muted);
                Label(T("весь ряд", "whole row"), legendX, HudLayout.LegendIcon(1).y, unit, 0.4f, TextAnchor.MiddleCenter, Muted);
            }

            DrawPopups(unit);
            DrawAchievementToast(unit);

            DrawPauseButton(unit);
            if (game.State == GameState.Paused)
                DrawPauseMenu(unit);
            else if (game.State == GameState.GameOver && game.Mode == GameMode.Battle)
                Overlay(T("БОЙ ОКОНЧЕН", "BATTLE OVER"), T("Счёт: ", "Score: ") + game.Score.ToString("N0") + "\n" + PlaceText() +
                    T("Ботов: ", "Bots: ") + battle.BotsDefeated + T("    монет: +", "    coins: +") + battle.CoinsEarned, unit);
            else if (game.State == GameState.GameOver)
                Overlay(T("ИГРА ОКОНЧЕНА", "GAME OVER"), T("Счёт: ", "Score: ") + game.Score.ToString("N0") + "\n" + PlaceText().TrimEnd('\n'), unit);
        }

        static readonly Color Muted = new Color(0.68f, 0.65f, 0.76f);
        static readonly Color Bright = new Color(0.96f, 0.94f, 0.98f);

        void Stat(string caption, string value, StatSlot slot, float unit, float valueScale = 1f)
        {
            Vector2 at = HudLayout.Stat(slot);
            float scale = HudLayout.StatScale(slot);
            Label(caption, at.x, at.y + 0.55f * scale, unit, 0.5f * scale, TextAnchor.MiddleCenter, Muted);
            Label(value, at.x, at.y - 0.25f * scale, unit, 0.85f * scale * valueScale, TextAnchor.MiddleCenter, Bright);
        }

        void DrawPopups(float unit)
        {
            foreach (Popup popup in popups)
            {
                float age = 1f - popup.Life / popup.MaxLife;
                float punch = age < 0.12f ? Mathf.Lerp(1.5f, 1f, age / 0.12f) : 1f;
                float alpha = Mathf.Clamp01(popup.Life / (popup.MaxLife * 0.4f));
                float size = popup.Size * punch;
                Color shadow = new Color(0f, 0f, 0f, alpha * 0.85f);
                Label(popup.Text, popup.X + 0.07f, popup.Y - 0.07f, unit, size, TextAnchor.MiddleCenter, shadow);
                Label(popup.Text, popup.X, popup.Y, unit, size, TextAnchor.MiddleCenter,
                    new Color(popup.Color.r, popup.Color.g, popup.Color.b, alpha));
            }
        }

        void Overlay(string title, string subtitle, float unit)
        {
            // Tall enough for the result lines and the two buttons under them.
            Vector2 min = WorldToGui(BoardView.BoardLeft, -5.85f);
            Vector2 max = WorldToGui(-BoardView.BoardLeft, 3.6f);
            GUI.color = new Color(0.07f, 0.05f, 0.11f, 0.92f);
            GUI.DrawTexture(new Rect(min.x, max.y, max.x - min.x, min.y - max.y), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Label(title, 0f, 2.45f, unit, 0.95f, TextAnchor.MiddleCenter, view.AccentColor);
            Label(subtitle, 0f, -0.55f, unit, 0.52f, TextAnchor.MiddleCenter, Bright);
            DrawGameOverButtons(unit);
        }

        void Label(string text, float worldX, float worldY, float unit, float sizeInUnits, TextAnchor anchor, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = anchor,
                fontSize = Mathf.Max(8, Mathf.RoundToInt(sizeInUnits * unit)),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            style.normal.textColor = GuiColor(color);

            Vector2 center = WorldToGui(worldX, worldY);
            float width = 26f * unit;
            // Tall enough for every line, so multi-line text is never clipped.
            int lines = text.Split('\n').Length;
            float height = Mathf.Max(3f, lines * sizeInUnits * 1.5f) * unit;
            GUI.Label(new Rect(center.x - width / 2f, center.y - height / 2f, width, height), text, style);
        }

        Vector2 WorldToGui(float x, float y)
        {
            Vector3 screen = mainCamera.WorldToScreenPoint(new Vector3(x, y, 0f));
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        // Testing aids: -autoplay plays random moves; -capture <file> [-captureAfter <seconds>] screenshots and quits;
        // -noRecords never saves a best score; -assist prepares rows so each hard drop clears lines (implies -noRecords);
        // -specialCycle makes every piece special in turn (implies -noRecords); -battle starts those runs as a battle;
        // -recordsTest saves results under separate test keys, even with -assist, so real records stay untouched;
        // -resetRecordsTest empties those test keys first.
        void ReadCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string capturePath = null;
            float captureAfter = 3f;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoplay")
                {
                    autoplay = true;
                    autoplayRandom = new System.Random(12345);
                }
                else if (args[i] == "-logSlowFrames")
                {
                    logSlowFrames = true;
                }
                else if (args[i] == "-battle")
                {
                    mode = GameMode.Battle;
                }
                else if (args[i] == "-noRecords")
                {
                    noRecords = true;
                }
                else if (args[i] == "-specialCycle")
                {
                    specialCycle = true;
                    specialCyclePending = true;
                    noRecords = true;
                }
                else if (args[i] == "-assist")
                {
                    assist = true;
                    noRecords = true;
                }
                else if (args[i] == "-capture" && i + 1 < args.Length)
                {
                    capturePath = args[i + 1];
                }
                else if (args[i] == "-captureAfter" && i + 1 < args.Length)
                {
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out captureAfter);
                }
            }
            if (System.Array.IndexOf(args, "-recordsTest") >= 0)
            {
                recordsPrefix = TestRecordsPrefix;
                noRecords = false;
                if (System.Array.IndexOf(args, "-resetRecordsTest") >= 0)
                {
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + BestScoreName);
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + MarathonTableName);
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + BattleTableName);
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + CoinsName);
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + ShopName);
                    PlayerPrefs.DeleteKey(TestRecordsPrefix + AchievementsName);
                    PlayerPrefs.Save();
                }
            }
            if (capturePath != null)
                StartCoroutine(CaptureAndQuit(capturePath, captureAfter));
        }

        void RunAutoplay()
        {
            autoplayTimer -= Time.deltaTime;
            if (autoplayTimer > 0f || game.State != GameState.Playing)
                return;
            autoplayTimer = 0.04f;
            game.Rotate(autoplayRandom.Next(2) * 2 - 1);
            game.Move(autoplayRandom.Next(2) * 2 - 1);
            if (autoplayRandom.Next(4) == 0)
                game.HardDrop();
        }

        IEnumerator CaptureAndQuit(string path, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}
