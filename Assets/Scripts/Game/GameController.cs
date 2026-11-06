using System;
using System.Collections.Generic;
using Lines.Core;

namespace Lines.Game
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
    public class GameController
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

        public GameController(GameState game, IBoardPresenter presenter)
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

            if (!Game.Board.IsEmpty(cell))
            {
                // Tap a ball: select it, or switch to it. Tapping the selected ball again does nothing.
                if (Selected != cell) Select(cell);
                return true;
            }

            if (Selected is Cell from) StartMove(from, cell);
            return true;
        }

        /// <summary>Drops the selection (when leaving the game screen).</summary>
        public void ClearSelection()
        {
            if (State != TurnState.BallSelected) return;
            Select(null);
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
            presenter.PlaySpawn(result.Spawned, result.SpawnedColors, () =>
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
        }

        void SetState(TurnState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
