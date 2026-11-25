using System;
using GC26.Core;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Entry point (IMicrogame) and game loop (TDD 5): input → model → view, plus overlays and saves.
    /// The only class that talks to both the model and the view.
    /// </summary>
    public class Game2048Nastya : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_2048_nastya";
        public const int BoardSize = Board2048.DefaultSize;

        /// <summary>
        /// Layout version of the entry scene. ColoursBuilder2048 stamps it into the scene; bump it whenever the
        /// builder adds or rewires objects, and the editor rebuilds an older scene by itself.
        /// </summary>
        public const int SceneVersion = 5;

        /// <summary>T59: phones default to 30 FPS; the game asks for 60 while it runs and restores the old value.</summary>
        public const int TargetFrameRate = 60;

        /// <summary>Tests only: set before the scene loads so the player's real save is never touched.</summary>
        public static SaveStore2048 TestSaveStore;

        /// <summary>Tests only: start without the splash screen.</summary>
        public static bool TestSkipSplash;

        [SerializeField] Theme2048 theme;
        [SerializeField] BoardView2048 boardView;
        [SerializeField] Hud2048 hud;
        [SerializeField] Overlays2048 overlays;
        [SerializeField] Input2048 input;
        [SerializeField] Sfx2048 sfx;
        [SerializeField] Splash2048 splash;

        [Header("Standalone only (disabled when the shell has its own camera)")]
        [SerializeField] Camera ownCamera;
        [SerializeField] Canvas canvas;

        [SerializeField, HideInInspector] int sceneVersion; // written by ColoursBuilder2048

        Action<int> _exit;
        bool _started;
        bool _exited;
        bool _hintVisible;
        GameState2048 _state;
        TileSpawner2048 _spawner;
        SaveStore2048 _save;
        readonly MoveResult2048 _result = new MoveResult2048();
        Direction2048? _buffered;
        Action _onMoveShown;
        int _previousFrameRate;
        bool _frameRateSet;

        public GameState2048 State => _state;

        /// <summary>The game is on screen and accepts moves: no overlay, no dialog, not exited.</summary>
        public bool CanPlay => _state != null && !_exited && !overlays.IsOpen && !hud.IsConfirmOpen && !SplashBlocking;

        bool SplashBlocking => splash != null && splash.IsBlocking;

        /// <summary>GDD 4.0: any key or move input during the splash only skips it.</summary>
        bool SkipSplash()
        {
            if (!SplashBlocking) return false;
            splash.Skip();
            return true;
        }

        public void Begin(Action<int> exit)
        {
            if (overlays == null || boardView == null || hud == null || input == null)
            {
                Debug.LogError($"[{Id}] the scene is out of date. Stop Play mode, then in the top menu click " +
                               "GC26 → Colours (nastya) → Rebuild assets and scene, and press Play again.");
                enabled = false;
                return;
            }

            _started = true;
            _exit = exit;
            _onMoveShown = OnMoveShown; // one delegate for every move, no allocation per move
            UseShellCamera();
            SetFrameRate();

            _spawner = new TileSpawner2048(new System.Random());
            _save = TestSaveStore ?? SaveStore2048.ForGame();
            boardView.Build(BoardSize, theme);
            ApplySound();

            hud.PauseClicked += Pause;
            hud.NewGameClicked += OnNewGameRequested;
            hud.UndoClicked += Undo;
            overlays.KeepGoingClicked += KeepGoing;
            overlays.TryAgainClicked += StartNewGame;
            overlays.ResumeClicked += Resume;
            overlays.NewGameClicked += OnOverlayNewGame;
            overlays.ToMenuClicked += ExitToMenu;
            overlays.SoundClicked += ToggleSound;
            input.Moved += OnMoveInput;
            input.PausePressed += Pause;
            input.NewGamePressed += OnNewGameRequested;
            input.UndoPressed += Undo;

            LoadOrStartGame();
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(StandaloneExit);
        }

        /// <summary>
        /// Without the shell, "To menu" acts like closing and launching the game again:
        /// the saved game is loaded back, so resuming (T44) can be checked in the editor.
        /// </summary>
        void StandaloneExit(int score)
        {
            Debug.Log($"[{Id}] exit, score = {score} (standalone: launching again from the save)");
            _exit = StandaloneExit;
            _exited = false;
            LoadOrStartGame();
        }

        void OnDisable() => RestoreFrameRate();

        void OnDestroy()
        {
            RestoreFrameRate();
            if (!_started) return;
            // Any of these may already be gone (or never assigned in an old scene): never throw here.
            if (hud != null)
            {
                hud.PauseClicked -= Pause;
                hud.NewGameClicked -= OnNewGameRequested;
                hud.UndoClicked -= Undo;
            }
            if (overlays != null)
            {
                overlays.KeepGoingClicked -= KeepGoing;
                overlays.TryAgainClicked -= StartNewGame;
                overlays.ResumeClicked -= Resume;
                overlays.NewGameClicked -= OnOverlayNewGame;
                overlays.ToMenuClicked -= ExitToMenu;
                overlays.SoundClicked -= ToggleSound;
            }
            if (input != null)
            {
                input.Moved -= OnMoveInput;
                input.PausePressed -= Pause;
                input.NewGamePressed -= OnNewGameRequested;
                input.UndoPressed -= Undo;
            }
        }

        // ---------- start ----------

        /// <summary>T44: continue the saved game, or start a new one when there is none (or it had ended).</summary>
        void LoadOrStartGame()
        {
            overlays.HideAll();
            hud.CloseConfirm();
            _buffered = null;

            int best = _save.LoadBest();
            var saved = _save.LoadGame(best);
            if (saved != null) Show(saved);
            else StartNewGame();

            // T47: the hint under the board, on the very first launch only; it goes away after the first move.
            _hintVisible = !_save.HintShown;
            hud.ShowHint(_hintVisible);
            if (_hintVisible) _save.HintShown = true;

            if (splash != null && !TestSkipSplash) splash.Show(null);
        }

        public void StartNewGame()
        {
            int best = Math.Max(_state?.Best ?? 0, _save.LoadBest());
            Show(GameState2048.NewGame(_spawner, best, BoardSize));
            _save.SaveGame(_state);
        }

        void Show(GameState2048 state)
        {
            overlays.HideAll();
            hud.CloseConfirm();
            _buffered = null;
            _state = state;
            boardView.Render(_state.Board);
            hud.SetScore(_state.Score, _state.Best);
            hud.SetUndoAvailable(_state.CanUndo);
            UpdateInput();
        }

        // ---------- moves ----------

        /// <summary>
        /// Input buffer (TDD 5.1, T32): during an animation one direction waits in <see cref="_buffered"/>.
        /// A second direction finishes the animation instantly, which plays the waiting one, and takes its place.
        /// So fast input never drops a move and the queue never holds more than one.
        /// </summary>
        public void OnMoveInput(Direction2048 dir)
        {
            if (SkipSplash() || !CanPlay) return;
            if (boardView.IsAnimating && _buffered.HasValue)
                boardView.FinishNow(); // plays the buffered move
            if (!CanPlay) return;      // that move may have opened the win or game over overlay
            if (boardView.IsAnimating) _buffered = dir;
            else Move(dir);
        }

        /// <summary>
        /// Plays one move right now (TDD 5.2): a running animation is finished first.
        /// Returns false for an invalid move (the board bumps). Public for tests.
        /// </summary>
        public bool Move(Direction2048 dir)
        {
            if (!CanPlay) return false;
            boardView.FinishNow();
            if (!CanPlay) return false;
            if (!_state.TryMove(dir, _spawner, _result, out var spawned))
            {
                if (!_state.IsOver)
                {
                    boardView.Bump(dir);
                    if (sfx != null) sfx.Invalid();
                }
                return false;
            }

            hud.SetScore(_state.Score, _state.Best);
            hud.SetUndoAvailable(_state.CanUndo && !_state.IsOver);
            _save.SaveGame(_state); // a finished game removes the save instead
            if (_hintVisible)
            {
                _hintVisible = false;
                hud.ShowHint(false);
            }
            PlayMoveSound();
            boardView.Play(_result, spawned, _state.Board, _onMoveShown);
            return true;
        }

        /// <summary>Completes the current animation and drops the buffered move (pause, exit, tests).</summary>
        public void FinishAnimation()
        {
            _buffered = null;
            boardView.FinishNow();
        }

        /// <summary>TDD 5.2 step 7: runs after the move animation, then plays the buffered move, if any.</summary>
        void OnMoveShown()
        {
            if (_state.ShouldShowWin(_result))
            {
                // T40: once per game. Game over (if it is also true) waits until "Keep going".
                _state.WonShown = true;
                _buffered = null;
                _save.SaveGame(_state);
                if (sfx != null) sfx.Win();
                overlays.ShowWin();
                UpdateInput();
                return;
            }
            if (_state.IsOver)
            {
                ShowGameOver();
                return;
            }

            if (!_buffered.HasValue) return;
            var next = _buffered.Value;
            _buffered = null;
            Move(next);
        }

        void ShowGameOver()
        {
            _buffered = null;
            _save.Flush();
            if (sfx != null) sfx.Lose();
            overlays.ShowGameOver(_state.Score, _state.Best);
            UpdateInput();
        }

        /// <summary>T54: U or the Undo button. One step only; a new game or a resumed game has nothing to undo.</summary>
        public void Undo()
        {
            if (SkipSplash() || !CanPlay) return;
            FinishAnimation();
            if (!CanPlay || !_state.TryUndo()) return;
            boardView.Render(_state.Board);
            hud.SetScore(_state.Score, _state.Best);
            hud.SetUndoAvailable(false);
            _save.SaveGame(_state);
        }

        // ---------- overlays and dialogs ----------

        void OnNewGameRequested()
        {
            if (SkipSplash() || _state == null || _exited || overlays.IsOpen) return;
            // Ask only when there is progress to lose (GDD 3).
            if (_state.Moves > 0 && !_state.IsOver)
                hud.ShowConfirm(StartNewGame);
            else
                StartNewGame();
        }

        void OnOverlayNewGame()
        {
            var from = overlays.Open;
            overlays.HideAll();
            UpdateInput();
            if (from == Overlays2048.Kind.Win) StartNewGame(); // the overlay itself was the choice
            else OnNewGameRequested();                           // from pause: confirm if progress would be lost
        }

        /// <summary>T40: "Keep going" closes the win overlay; the game continues with no limit.</summary>
        public void KeepGoing()
        {
            if (overlays.Open != Overlays2048.Kind.Win) return;
            overlays.HideAll();
            if (_state.IsOver) ShowGameOver();
            else UpdateInput();
        }

        /// <summary>T42: Esc or the Pause button. Toggles the pause menu; closes the "new game?" dialog first.</summary>
        public void Pause()
        {
            if (SkipSplash() || _state == null || _exited) return;
            if (hud.IsConfirmOpen) { hud.CloseConfirm(); return; }
            if (overlays.Open == Overlays2048.Kind.Pause) { Resume(); return; }
            if (overlays.IsOpen) return; // win / game over have their own buttons

            FinishAnimation();
            if (overlays.IsOpen) return; // the finished move opened the win or game over overlay
            overlays.ShowPause();
            _save.SaveGame(_state);
            _save.Flush();
            UpdateInput();
        }

        public void Resume()
        {
            if (overlays.Open != Overlays2048.Kind.Pause) return;
            overlays.HideAll();
            UpdateInput();
        }

        /// <summary>Moves, N and U are ignored while an overlay is open; Esc keeps working. Time.timeScale is never touched.</summary>
        void UpdateInput()
        {
            input.GameplayEnabled = !_exited && !overlays.IsOpen;
        }

        // ---------- sound (T52, T53) ----------

        void PlayMoveSound()
        {
            if (sfx == null) return;
            int biggest = 0;
            foreach (var merge in _result.Merges)
                if (merge.NewTile.Value > biggest) biggest = merge.NewTile.Value;
            if (biggest > 0) sfx.Merge(biggest);
            else sfx.Slide();
        }

        /// <summary>T53: the pause menu toggle; saved at once, so it survives a restart.</summary>
        public void ToggleSound()
        {
            _save.SoundOn = !_save.SoundOn;
            ApplySound();
        }

        void ApplySound()
        {
            bool on = _save.SoundOn;
            if (sfx != null) sfx.Muted = !on;
            overlays.SetSound(on);
        }

        // ---------- frame rate (rule 6: restore what we change) ----------

        void SetFrameRate()
        {
            if (_frameRateSet) return;
            _previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = TargetFrameRate;
            _frameRateSet = true;
        }

        void RestoreFrameRate()
        {
            if (!_frameRateSet) return;
            Application.targetFrameRate = _previousFrameRate;
            _frameRateSet = false;
        }

#if UNITY_EDITOR
        // ---------- T55: debug boards, editor only (right-click the component in Play mode) ----------

        [ContextMenu("Debug: board one move from 2048 (press Left)")]
        void DebugAlmostWin() => DebugSetBoard(new[]
        {
            1024, 1024, 0, 0,
            512, 256, 128, 64,
            0, 0, 0, 0,
            2, 0, 0, 0,
        }, wonShown: false);

        [ContextMenu("Debug: board one move from game over (press Left)")]
        void DebugAlmostGameOver() => DebugSetBoard(new[]
        {
            0, 8, 16, 8,
            32, 64, 32, 64,
            128, 256, 128, 256,
            512, 1024, 512, 1024,
        }, wonShown: _state != null && _state.WonShown);

        void DebugSetBoard(int[] values, bool wonShown)
        {
            if (!Application.isPlaying || _state == null) { Debug.LogWarning($"[{Id}] debug boards work in Play mode"); return; }
            FinishAnimation();
            Show(new GameState2048(Board2048.FromValues(BoardSize, values), _state.Score, _state.Best, _state.Moves, wonShown));
            _save.SaveGame(_state);
        }
#endif

        // ---------- shell ----------

        /// <summary>Inside the shell, render the canvas with the shell's camera and switch ours off (TDD 9).</summary>
        void UseShellCamera()
        {
            if (!MicrogameRunner.IsRunning || ownCamera == null) return;
            var shell = Camera.main;
            if (shell == null || shell == ownCamera) return;
            ownCamera.gameObject.SetActive(false);
            if (canvas != null) canvas.worldCamera = shell;
        }

        /// <summary>T46: "To menu" in the pause menu or on game over. Saves, then calls exit(score) exactly once.</summary>
        public void ExitToMenu()
        {
            if (_state == null || _exited) return;
            FinishAnimation();
            _exited = true;
            UpdateInput();
            _save.SaveGame(_state);
            _save.Flush();
            var exit = _exit;
            _exit = null;
            exit?.Invoke(_state.Score);
        }

        /// <summary>Phones may kill a game in the background without warning, so save when it goes there.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        void SaveNow()
        {
            if (!_started || _exited || _save == null) return;
            _save.SaveGame(_state);
            _save.Flush();
        }
    }
}
