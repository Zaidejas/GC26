using System;
using System.Collections.Generic;

namespace GC26.Lines.Core
{
    /// <summary>What happened during one move. <see cref="Moved"/> is false when there was no path.</summary>
    public sealed class MoveResult
    {
        public bool Moved { get; internal set; }
        /// <summary>The cells the ball passed through, start and target included (empty if blocked).</summary>
        public IReadOnlyList<Cell> Path { get; internal set; } = Array.Empty<Cell>();
        public IReadOnlyList<Line> LinesFromMove { get; internal set; } = Array.Empty<Line>();
        public int MovePoints { get; internal set; }
        /// <summary>Cells where new balls appeared (empty when the move cleared a line).</summary>
        public IReadOnlyList<Cell> Spawned { get; internal set; } = Array.Empty<Cell>();
        /// <summary>The color of each spawned ball, in the same order as <see cref="Spawned"/>
        /// (kept even if the balls then formed a line and were cleared).</summary>
        public IReadOnlyList<BallColor> SpawnedColors { get; internal set; } = Array.Empty<BallColor>();
        public IReadOnlyList<Line> LinesFromSpawn { get; internal set; } = Array.Empty<Line>();
        public int SpawnPoints { get; internal set; }
        public bool GameOver { get; internal set; }
    }

    /// <summary>The position before the last move, for Undo: board, score and preview.</summary>
    public sealed class UndoSnapshot
    {
        public Board Board { get; }
        public int Score { get; }
        public Preview Preview { get; }

        public UndoSnapshot(Board board, int score, Preview preview)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Score = score;
        }
    }

    /// <summary>
    /// One game of Lines: the board, score, preview and the turn rules.
    /// A turn: move a ball along a free path → if it completes lines, clear them (no spawn);
    /// otherwise spawn the 3 preview balls → clear lines they complete → game over if the board is full,
    /// else choose the next preview. Same flow as move() / spawn() in docs/prototype/lines-prototype.html.
    /// The view layer listens to the events and plays animations; it never changes the board itself.
    /// </summary>
    public class GameState
    {
        public Board Board { get; }
        public int Score { get; private set; }
        public Preview Preview { get; private set; }
        public bool IsOver { get; private set; }
        public Spawner Spawner { get; }

        /// <summary>
        /// The position before the last move, or null. Undo is one step only, as in the classic game:
        /// there is none at the start of a game, right after an undo, or after game over (GDD → Controls).
        /// </summary>
        public UndoSnapshot UndoStep { get; private set; }
        public bool CanUndo => UndoStep != null && !IsOver;

        /// <summary>A ball moved: from, to, and the path it took.</summary>
        public event Action<Cell, Cell, IReadOnlyList<Cell>> OnBallMoved;
        /// <summary>The player tried to move a ball where it can't go: from, to.</summary>
        public event Action<Cell, Cell> OnMoveBlocked;
        /// <summary>Lines were cleared: the lines, the cells removed, the points, and whether the player's move made them.</summary>
        public event Action<IReadOnlyList<Line>, IReadOnlyList<Cell>, int, bool> OnLinesCleared;
        /// <summary>New balls appeared in these cells.</summary>
        public event Action<IReadOnlyList<Cell>> OnBallsSpawned;
        /// <summary>The board is full; the final score.</summary>
        public event Action<int> OnGameOver;
        /// <summary>The last move was taken back.</summary>
        public event Action OnUndone;

        /// <summary>Starts a new game: 3 random balls and the first preview.</summary>
        public static GameState NewGame(int seed)
        {
            var spawner = new Spawner(seed);
            var board = new Board();
            spawner.PlaceStartBalls(board);
            return new GameState(board, spawner.MakePreview(board), 0, spawner);
        }

        /// <summary>Continues a game from a given position (used by saving and by tests).</summary>
        public GameState(Board board, Preview preview, int score, Spawner spawner, bool isOver = false, UndoSnapshot undoStep = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            Score = score;
            IsOver = isOver;
            UndoStep = isOver ? null : undoStep;
        }

        /// <summary>
        /// Takes back the last move: board, score and preview return to how they were before it.
        /// Returns false (and changes nothing) when there is nothing to undo.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            Board.CopyFrom(UndoStep.Board);
            Score = UndoStep.Score;
            Preview = UndoStep.Preview;
            UndoStep = null;
            OnUndone?.Invoke();
            return true;
        }

        /// <summary>
        /// Plays one turn: moves the ball at <paramref name="from"/> to the empty cell <paramref name="to"/>.
        /// If there is no free path nothing changes, <see cref="OnMoveBlocked"/> fires and Moved is false.
        /// </summary>
        public MoveResult TryMove(Cell from, Cell to)
        {
            if (IsOver) throw new InvalidOperationException("The game is over.");
            if (Board.IsEmpty(from)) throw new ArgumentException($"There is no ball at {from}.", nameof(from));
            if (!Board.IsEmpty(to)) throw new ArgumentException($"Cell {to} is not empty.", nameof(to));

            var result = new MoveResult();
            var path = Pathfinder.FindPath(Board, from, to);
            if (path == null)
            {
                OnMoveBlocked?.Invoke(from, to);
                return result;
            }

            UndoStep = new UndoSnapshot(Board.Clone(), Score, Preview); // Preview is immutable, so it can be kept as is
            Board.Move(from, to);
            result.Moved = true;
            result.Path = path;
            OnBallMoved?.Invoke(from, to, path);

            var moveLines = LineDetector.FindLinesThrough(Board, to);
            if (moveLines.Count > 0)
            {
                // A cleared line skips the spawn; the preview stays the same for the next turn.
                result.LinesFromMove = moveLines;
                result.MovePoints = Clear(moveLines, ScoreCalculator.ForMove(moveLines), byMove: true);
                return result;
            }

            var spawned = Spawner.Spawn(Board, Preview);
            result.Spawned = spawned;
            var spawnedColors = new List<BallColor>(spawned.Count);
            foreach (var cell in spawned) spawnedColors.Add(Board.Get(cell));
            result.SpawnedColors = spawnedColors;
            OnBallsSpawned?.Invoke(spawned);

            var spawnLines = LineDetector.FindLinesThrough(Board, spawned);
            if (spawnLines.Count > 0)
            {
                result.LinesFromSpawn = spawnLines;
                result.SpawnPoints = Clear(spawnLines, ScoreCalculator.ForSpawn(spawnLines), byMove: false);
            }

            if (Board.IsFull)
            {
                IsOver = true;
                UndoStep = null; // game over is final
                result.GameOver = true;
                OnGameOver?.Invoke(Score);
            }
            else
            {
                Preview = Spawner.MakePreview(Board);
            }
            return result;
        }

        int Clear(List<Line> lines, int points, bool byMove)
        {
            var cells = LineDetector.CellsToClear(lines);
            foreach (var cell in cells) Board.Remove(cell);
            Score += points;
            OnLinesCleared?.Invoke(lines, cells, points, byMove);
            return points;
        }
    }
}
