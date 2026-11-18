using System;
using GC26.Core;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Entry point (IMicrogame) and game loop (TDD 5): input → model → view.
    /// The only class that talks to both the model and the view.
    /// </summary>
    public class Game2048Nastya : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_2048_nastya";
        public const int BoardSize = Board2048.DefaultSize;

        [SerializeField] Theme2048 theme;
        [SerializeField] BoardView2048 boardView;
        [SerializeField] Hud2048 hud;
        [SerializeField] Input2048 input;

        [Header("Standalone only (disabled when the shell has its own camera)")]
        [SerializeField] Camera ownCamera;
        [SerializeField] Canvas canvas;

        Action<int> _exit;
        bool _started;
        GameState2048 _state;
        TileSpawner2048 _spawner;
        readonly MoveResult2048 _result = new MoveResult2048();

        public GameState2048 State => _state;

        public void Begin(Action<int> exit)
        {
            _started = true;
            _exit = exit;
            UseShellCamera();

            _spawner = new TileSpawner2048(new System.Random());
            // TODO (M4, T43-T44): load the best score and the game in progress from MicrogameSave.Folder(Id)
            boardView.Build(BoardSize, theme);
            StartNewGame();

            hud.PauseClicked += OnPause;
            hud.NewGameClicked += OnNewGameRequested;
            input.Moved += OnMoveInput;
            input.PausePressed += OnPause;
            input.NewGamePressed += OnNewGameRequested;
            input.UndoPressed += Undo;
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        void OnDestroy()
        {
            if (!_started) return;
            hud.PauseClicked -= OnPause;
            hud.NewGameClicked -= OnNewGameRequested;
            input.Moved -= OnMoveInput;
            input.PausePressed -= OnPause;
            input.NewGamePressed -= OnNewGameRequested;
            input.UndoPressed -= Undo;
        }

        void OnMoveInput(Direction2048 dir) => Move(dir);

        /// <summary>Plays one move (TDD 5.2). Called by Input2048; public for tests.</summary>
        public bool Move(Direction2048 dir)
        {
            if (_state == null || hud.IsConfirmOpen) return false;
            if (!_state.TryMove(dir, _spawner, _result, out _))
                return false; // TODO (M3, T34): bump the board

            // TODO (M3, T30): animate _result instead of redrawing
            boardView.Render(_state.Board);
            hud.SetScore(_state.Score, _state.Best);

            if (_state.ShouldShowWin(_result))
            {
                _state.WonShown = true; // TODO (M4, T40): win overlay with Keep going / New game
                Debug.Log($"[{Id}] 2048 reached");
            }
            if (_state.IsOver)
                Debug.Log($"[{Id}] game over, score = {_state.Score}"); // TODO (M4, T41): game over overlay
            return true;
        }

        public void StartNewGame()
        {
            hud.CloseConfirm();
            _state = GameState2048.NewGame(_spawner, _state?.Best ?? 0, BoardSize);
            boardView.Render(_state.Board);
            hud.SetScore(_state.Score, _state.Best);
        }

        void OnNewGameRequested()
        {
            // Ask only when there is progress to lose (GDD 3).
            if (_state != null && _state.Moves > 0 && !_state.IsOver)
                hud.ShowConfirm(StartNewGame);
            else
                StartNewGame();
        }

        void Undo()
        {
            if (hud.IsConfirmOpen || !_state.TryUndo()) return;
            boardView.Render(_state.Board);
            hud.SetScore(_state.Score, _state.Best);
        }

        void OnPause()
        {
            if (hud.IsConfirmOpen) { hud.CloseConfirm(); return; }
            // TODO (M4, T42): pause menu with Resume / New game / To menu
            Debug.Log($"[{Id}] pause (menu comes in M4)");
        }

        /// <summary>Inside the shell, render the canvas with the shell's camera and switch ours off (TDD 9).</summary>
        void UseShellCamera()
        {
            if (!MicrogameRunner.IsRunning || ownCamera == null) return;
            var shell = Camera.main;
            if (shell == null || shell == ownCamera) return;
            ownCamera.gameObject.SetActive(false);
            if (canvas != null) canvas.worldCamera = shell;
        }

        /// <summary>Call from the pause menu ("To menu") or the game-over screen.</summary>
        public void ExitToMenu()
        {
            // TODO (M4, T46): save progress before leaving
            var exit = _exit;
            _exit = null;
            exit?.Invoke(_state?.Score ?? 0);
        }
    }
}
