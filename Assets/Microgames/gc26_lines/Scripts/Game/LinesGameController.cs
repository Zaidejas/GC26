using System;
using System.Collections.Generic;
using GC26.Lines.Core;

namespace GC26.Lines.Game
{
    /// <summary>Where the turn is (GDD → Controls; Plan → Architecture).</summary>
    public enum TurnState
    {
        /// <summary>Waiting for the player; no ball selected.</summary>
        Idle,
        /// <summary>A ball is selected (bouncing); waiting for a target cell.</summary>
        BallSelected,
        /// <summary>The ball is rolling along its path.</summary>
        Moving,
        /// <summary>Completed lines are disappearing.</summary>
        Resolving,
        /// <summary>New balls are appearing.</summary>
        Spawning,
        /// <summary>The board is full; no more moves.</summary>
        GameOver,
    }

    /// <summary>
    /// Turns taps into game moves and runs the turn step by step:
    /// Idle → BallSelected → Moving → Resolving (lines from the move) or Spawning → Resolving (lines from
    /// spawned balls) → game-over check → Idle / GameOver.
    /// While a move, clear or spawn animation plays, taps are ignored (GDD: input is locked during animations).
    /// The rules themselves live in <see cref="GameState"/>; this class only decides what to show and when.
    /// </summary>
    public class LinesGameController
    {
        public GameState Game { get; }
        public TurnState State { get; private set; }
        public Cell? Selected { get; private set; }

        readonly IBoardPresenter presenter;

        /// <summary>True when a tap would be handled (no animation playing, game not over).</summary>
        public bool AcceptsInput => State == TurnState.Idle || State == TurnState.BallSelected;

        public event Action<TurnState> StateChanged;
        /// <summary>The game's position changed and settled: a turn finished, or a move was undone (time to save).</summary>
        public event Action GameChanged;
        /// <summary>Timed mode: a turn's cleared lines added these seconds to the clock.</summary>
        public event Action<float> TimeAdded;
        /// <summary>L16: a turn's points earned this many puffballs.</summary>
        public event Action<int> PuffballsEarned;
        /// <summary>L16: no ball can move, but a puffball is in stock: offer it (the game isn't over).</summary>
        public event Action StuckWithPuffball;
        /// <summary>L16: aiming a puffball started (true) or ended (false).</summary>
        public event Action<bool> AimingChanged;
        /// <summary>L15: the player tapped a frozen ball.</summary>
        public event Action<Cell> FrozenTapped;
        MoveResult lastMove;

        /// <summary>L16: a puffball is being aimed; <see cref="AimCell"/> is the middle of the 3×3 square.</summary>
        public bool Aiming { get; private set; }
        public Cell AimCell { get; private set; }

        public LinesGameController(GameState game, IBoardPresenter presenter)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            State = game.IsOver ? TurnState.GameOver : TurnState.Idle;
            presenter.ShowState(game);
        }

        /// <summary>
        /// Handles a tap on <paramref name="cell"/>. Returns false when the tap was ignored
        /// (an animation is playing or the game is over).
        /// </summary>
        public bool Tap(Cell cell)
        {
            if (!AcceptsInput) return false;
            if (!Game.Board.IsInside(cell)) return true;

            if (Aiming)
            {
                // L16: a tap on the lit spot bursts it; a tap anywhere else moves the square there.
                if (cell == AimCell) Burst();
                else AimAt(cell);
                return true;
            }

            if (!Game.Board.IsEmpty(cell) && Game.Board.IsFrozen(cell))
            {
                // L15: a frozen ball can't be picked up: it shakes; a selected ball stays selected.
                presenter.ShowFrozen(cell);
                FrozenTapped?.Invoke(cell);
                return true;
            }

            if (!Game.Board.IsEmpty(cell))
            {
                // Tap a ball: select it, or switch to it. Tapping the selected ball again does nothing.
                if (Selected != cell) Select(cell);
                return true;
            }

            if (Selected is Cell from) StartMove(from, cell);
            return true;
        }

        /// <summary>Drops the selection and any aiming (when leaving the game screen, pausing, opening Settings).</summary>
        public void ClearSelection()
        {
            CancelAiming();
            if (State != TurnState.BallSelected) return;
            Select(null);
        }

        /// <summary>
        /// L16: the puffball button: starts aiming on the spot with the most balls. Returns false (nothing happens)
        /// with no puffball in stock, during an animation or after game over.
        /// </summary>
        public bool StartAiming()
        {
            if (Aiming) return true;
            if (!AcceptsInput || Game.Puffballs <= 0) return false;
            if (Selected.HasValue) Select(null);
            Aiming = true;
            AimCell = Game.BestBurstCell();
            presenter.ShowAim(AimCell);
            AimingChanged?.Invoke(true);
            return true;
        }

        /// <summary>L16: moves the aimed 3×3 square to <paramref name="cell"/> (a tap elsewhere, or a finger sliding).</summary>
        public void AimAt(Cell cell)
        {
            if (!Aiming || !Game.Board.IsInside(cell) || cell == AimCell) return;
            AimCell = cell;
            presenter.ShowAim(cell);
        }

        /// <summary>L16: stops aiming without bursting (the puffball stays).</summary>
        public void CancelAiming()
        {
            if (!Aiming) return;
            Aiming = false;
            presenter.ShowAim(null);
            AimingChanged?.Invoke(false);
        }

        /// <summary>L16: bursts the aimed square. An empty square can't be burst (returns false, aiming goes on).</summary>
        public bool Burst()
        {
            if (!Aiming || !AcceptsInput) return false;
            var result = Game.Burst(AimCell);
            if (result == null) return false;
            Aiming = false;
            presenter.ShowAim(null);
            AimingChanged?.Invoke(false);
            SetState(TurnState.Resolving);
            presenter.PlayBurst(result, FinishTurn);
            return true;
        }

        /// <summary>L16: stuck with a puffball, the player chose End game.</summary>
        public void EndGame()
        {
            if (Game.IsOver) return;
            CancelAiming();
            Selected = null;
            presenter.ShowSelected(null);
            Game.EndGame();
            SetState(TurnState.GameOver);
            presenter.ShowGameOver(Game.Score);
            GameChanged?.Invoke();
        }

        /// <summary>
        /// Timed mode: runs the clock down by <paramref name="seconds"/>, only while the player is thinking (no
        /// animation playing). Returns true when the time ran out: the game is over (the caller shows "Time's up!").
        /// </summary>
        public bool AdvanceClock(float seconds)
        {
            if (!Game.Timed || !AcceptsInput) return false;
            if (!Game.AdvanceClock(seconds)) return false;
            CancelAiming();
            Selected = null;
            presenter.ShowSelected(null);
            SetState(TurnState.GameOver);
            GameChanged?.Invoke();
            return true;
        }

        /// <summary>True when the Undo button should be usable: something to undo and no animation playing.</summary>
        public bool CanUndo => AcceptsInput && Game.CanUndo;

        /// <summary>
        /// Takes back the last move (one step). Ignored while an animation plays, at the start of a game,
        /// right after an undo and after game over. Returns true when a move was taken back.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            CancelAiming();
            Selected = null;
            presenter.ShowSelected(null);
            Game.Undo();
            presenter.ShowState(Game);
            SetState(TurnState.Idle);
            GameChanged?.Invoke();
            return true;
        }

        void Select(Cell? cell)
        {
            Selected = cell;
            presenter.ShowSelected(cell);
            SetState(cell.HasValue ? TurnState.BallSelected : TurnState.Idle);
        }

        void StartMove(Cell from, Cell to)
        {
            if (!Pathfinder.HasPath(Game.Board, from, to))
            {
                // Blocked: the ball shakes and stays selected; no lock.
                presenter.ShowBlocked(from, to);
                Game.TryMove(from, to); // raises OnMoveBlocked; changes nothing
                return;
            }

            Selected = null;
            presenter.ShowSelected(null);
            var result = Game.TryMove(from, to); // the rules are applied at once; the screen catches up below
            lastMove = result;
            SetState(TurnState.Moving);
            presenter.PlayMove(result.Path, () => AfterMove(result));
        }

        void AfterMove(MoveResult result)
        {
            if (result.LinesFromMove.Count > 0)
            {
                SetState(TurnState.Resolving);
                presenter.PlayClear(LineDetector.CellsToClear(result.LinesFromMove), result.MovePoints, FinishTurn);
                return;
            }

            SetState(TurnState.Spawning);
            presenter.PlaySpawn(result.Spawned, result.SpawnedColors, result.SpawnedFrozen, () =>
            {
                if (result.LinesFromSpawn.Count > 0)
                {
                    SetState(TurnState.Resolving);
                    presenter.PlayClear(LineDetector.CellsToClear(result.LinesFromSpawn), result.SpawnPoints, FinishTurn);
                }
                else
                {
                    FinishTurn();
                }
            });
        }

        void FinishTurn()
        {
            presenter.ShowState(Game);
            if (lastMove != null && lastMove.TimeAdded > 0f) TimeAdded?.Invoke(lastMove.TimeAdded);
            int earned = lastMove != null ? lastMove.PuffballsEarned : 0;
            lastMove = null;
            if (Game.IsOver)
            {
                SetState(TurnState.GameOver);
                presenter.ShowGameOver(Game.Score);
            }
            else
            {
                SetState(TurnState.Idle);
            }
            GameChanged?.Invoke();
            if (earned > 0) PuffballsEarned?.Invoke(earned);
            if (Game.Stuck) StuckWithPuffball?.Invoke();
        }

        void SetState(TurnState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
