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
        /// <summary>L15: whether each spawned ball arrived frozen, in the same order as <see cref="Spawned"/>.</summary>
        public IReadOnlyList<bool> SpawnedFrozen { get; internal set; } = Array.Empty<bool>();
        public IReadOnlyList<Line> LinesFromSpawn { get; internal set; } = Array.Empty<Line>();
        public int SpawnPoints { get; internal set; }
        public bool GameOver { get; internal set; }
        /// <summary>Timed mode: the seconds added to the clock by the lines this move cleared.</summary>
        public float TimeAdded { get; internal set; }
        /// <summary>L16: puffballs earned by the points of this move.</summary>
        public int PuffballsEarned { get; internal set; }
    }

    /// <summary>L16: what a puffball burst removed.</summary>
    public sealed class BurstResult
    {
        /// <summary>The cell in the middle of the 3×3 square.</summary>
        public Cell Centre { get; internal set; }
        /// <summary>The balls that burst (cells that held a ball), and which of them were frozen.</summary>
        public IReadOnlyList<Cell> Cells { get; internal set; } = Array.Empty<Cell>();
        public IReadOnlyList<bool> WasFrozen { get; internal set; } = Array.Empty<bool>();
        public bool GameOver { get; internal set; }
    }

    /// <summary>The position before the last move or burst, for Undo: board, score, preview and puffballs.</summary>
    public sealed class UndoSnapshot
    {
        public Board Board { get; }
        public int Score { get; }
        public Preview Preview { get; }
        public int Puffballs { get; }
        public int NextPuffballAt { get; }

        public UndoSnapshot(Board board, int score, Preview preview, int puffballs = 0, int nextPuffballAt = 0)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Score = score;
            Puffballs = puffballs;
            NextPuffballAt = nextPuffballAt > 0 ? nextPuffballAt : GameState.PuffballMarkAfter(score);
        }
    }

    /// <summary>
    /// One game of Lines: the board, score, preview and the turn rules.
    /// A turn: move a ball along a free path → if it completes lines, clear them (no spawn);
    /// otherwise spawn the 3 preview balls → clear lines they complete → game over if no ball can move
    /// (unless a puffball can rescue it), else choose the next preview.
    /// Same flow as move() / spawn() / endTurn() in docs/prototype/lines-prototype.html.
    /// The view layer listens to the events and plays animations; it never changes the board itself.
    /// </summary>
    public class GameState
    {
        public Board Board { get; }
        public int Score { get; private set; }
        public Preview Preview { get; private set; }
        public bool IsOver { get; private set; }
        public Spawner Spawner { get; }

        /// <summary>Timed mode (GDD → Timed mode): a clock, no Undo, +5 s for every cleared line.</summary>
        public bool Timed { get; }
        /// <summary>Timed mode: the seconds left on the clock (0 in Classic).</summary>
        public float TimeLeft { get; private set; }
        /// <summary>Timed mode: the clock reached 0:00 (the game is over).</summary>
        public bool TimeUp => Timed && IsOver && TimeLeft <= 0f;

        /// <summary>L16: puffballs in stock (0 to <see cref="GameConfig.PuffballMax"/>).</summary>
        public int Puffballs { get; private set; }
        /// <summary>L16: the score at which the next puffball is earned (while the pocket isn't full).</summary>
        public int NextPuffballAt { get; private set; }
        /// <summary>L16: how far the next puffball is, 0 to 1 (1 while the pocket is full: the ring stays full).</summary>
        public float PuffballProgress => Puffballs >= GameConfig.PuffballMax ? 1f
            : Math.Max(0f, Math.Min(1f, 1f - (NextPuffballAt - Score) / (float)GameConfig.PuffballEvery));

        /// <summary>No ball can move: the board is full, or the free cells are walled in by frozen balls.</summary>
        public bool NoMovesLeft => Board.NoMovesLeft();
        /// <summary>L16: no ball can move but a puffball is in stock: the game offers it instead of ending.</summary>
        public bool Stuck => !IsOver && Puffballs > 0 && NoMovesLeft;
        /// <summary>The game ended with free cells left (walled in by ice): the pop-up says "No moves left".</summary>
        public bool EndedWithNoMoves => IsOver && !TimeUp && !Board.IsFull;

        /// <summary>L17: the tutorials this game has already shown (saved with it; a new game shows them again).</summary>
        public HashSet<string> TutorialsShown { get; } = new HashSet<string>();

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
        /// <summary>No ball can move any more (or time ran out); the final score.</summary>
        public event Action<int> OnGameOver;
        /// <summary>The last move was taken back.</summary>
        public event Action OnUndone;

        /// <summary>Starts a new game: 3 random balls and the first preview (never with a rainbow).</summary>
        public static GameState NewGame(int seed, bool timed = false)
        {
            var spawner = new Spawner(seed);
            var board = new Board();
            spawner.PlaceStartBalls(board);
            return new GameState(board, spawner.MakePreview(board, 0, rainbows: false), 0, spawner, timed: timed, timeLeft: GameConfig.TimedStart);
        }

        /// <summary>The first multiple of <see cref="GameConfig.PuffballEvery"/> above <paramref name="score"/>.</summary>
        public static int PuffballMarkAfter(int score) => (score / GameConfig.PuffballEvery + 1) * GameConfig.PuffballEvery;

        /// <summary>Continues a game from a given position (used by saving and by tests).</summary>
        public GameState(Board board, Preview preview, int score, Spawner spawner, bool isOver = false, UndoSnapshot undoStep = null,
            bool timed = false, float timeLeft = 0f, int puffballs = 0, int nextPuffballAt = 0)
        {
            Timed = timed;
            TimeLeft = timed ? Math.Max(0f, timeLeft) : 0f;
            if (timed) undoStep = null; // no Undo in Timed mode
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            Score = score;
            IsOver = isOver;
            UndoStep = isOver ? null : undoStep;
            Puffballs = Math.Max(0, Math.Min(GameConfig.PuffballMax, puffballs));
            NextPuffballAt = nextPuffballAt > score ? nextPuffballAt : PuffballMarkAfter(score);
        }

        /// <summary>
        /// Takes back the last move or burst: board, score, preview and puffballs return to how they were before it.
        /// Returns false (and changes nothing) when there is nothing to undo.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            Board.CopyFrom(UndoStep.Board);
            Score = UndoStep.Score;
            Preview = UndoStep.Preview;
            Puffballs = UndoStep.Puffballs;
            NextPuffballAt = UndoStep.NextPuffballAt;
            UndoStep = null;
            OnUndone?.Invoke();
            return true;
        }

        /// <summary>
        /// Timed mode: runs the clock down by <paramref name="seconds"/> of the player's thinking time (the caller stops
        /// it during animations and away from the game screen). At 0:00 the game is over ("Time's up!").
        /// Returns true on the call that ran the time out.
        /// </summary>
        public bool AdvanceClock(float seconds)
        {
            if (!Timed || IsOver || seconds <= 0f) return false;
            TimeLeft = Math.Max(0f, TimeLeft - seconds);
            if (TimeLeft > 0f) return false;
            EndNow();
            return true;
        }

        /// <summary>L16: the player chose End game instead of using a puffball to get unstuck.</summary>
        public void EndGame()
        {
            if (!IsOver) EndNow();
        }

        void EndNow()
        {
            IsOver = true;
            UndoStep = null; // game over is final
            OnGameOver?.Invoke(Score);
        }

        /// <summary>
        /// Plays one turn: moves the ball at <paramref name="from"/> to the empty cell <paramref name="to"/>.
        /// If there is no free path nothing changes, <see cref="OnMoveBlocked"/> fires and Moved is false.
        /// A frozen ball can't move (the controller never selects one).
        /// </summary>
        public MoveResult TryMove(Cell from, Cell to)
        {
            if (IsOver) throw new InvalidOperationException("The game is over.");
            if (Board.IsEmpty(from)) throw new ArgumentException($"There is no ball at {from}.", nameof(from));
            if (Board.IsFrozen(from)) throw new ArgumentException($"The ball at {from} is frozen.", nameof(from));
            if (!Board.IsEmpty(to)) throw new ArgumentException($"Cell {to} is not empty.", nameof(to));

            var result = new MoveResult();
            var path = Pathfinder.FindPath(Board, from, to);
            if (path == null)
            {
                OnMoveBlocked?.Invoke(from, to);
                return result;
            }

            if (!Timed) UndoStep = Snapshot();
            Board.Move(from, to);
            result.Moved = true;
            result.Path = path;
            OnBallMoved?.Invoke(from, to, path);
            int puffballsBefore = Puffballs;

            var moveLines = LineDetector.FindLinesThrough(Board, to);
            if (moveLines.Count > 0)
            {
                // A cleared line skips the spawn; the preview stays the same for the next turn.
                result.LinesFromMove = moveLines;
                result.MovePoints = Clear(moveLines, ScoreCalculator.ForMove(moveLines), byMove: true);
                result.TimeAdded = AddTime(moveLines.Count);
                result.PuffballsEarned = Puffballs - puffballsBefore;
                return result;
            }

            var spawnedFrozen = new List<bool>(Preview.Colors.Count);
            var spawned = Spawner.Spawn(Board, Preview);
            result.Spawned = spawned;
            var spawnedColors = new List<BallColor>(spawned.Count);
            foreach (var cell in spawned)
            {
                spawnedColors.Add(Board.Get(cell));
                spawnedFrozen.Add(Board.IsFrozen(cell));
            }
            result.SpawnedColors = spawnedColors;
            result.SpawnedFrozen = spawnedFrozen;
            OnBallsSpawned?.Invoke(spawned);

            var spawnLines = LineDetector.FindLinesThrough(Board, spawned);
            if (spawnLines.Count > 0)
            {
                result.LinesFromSpawn = spawnLines;
                result.SpawnPoints = Clear(spawnLines, ScoreCalculator.ForSpawn(spawnLines), byMove: false);
                result.TimeAdded = AddTime(spawnLines.Count);
            }
            result.PuffballsEarned = Puffballs - puffballsBefore;

            if (!Board.IsFull) Preview = Spawner.MakePreview(Board, Score);
            result.GameOver = EndIfNoMoves();
            return result;
        }

        /// <summary>
        /// L16: bursts the 3×3 square around <paramref name="centre"/> with a puffball. No points, and not a move:
        /// like a cleared line, no new balls come after it and the preview stays. Undo can take it back. Returns null
        /// (and changes nothing) with no puffball, after game over, or when the square holds no ball.
        /// </summary>
        public BurstResult Burst(Cell centre)
        {
            if (IsOver || Puffballs <= 0 || !Board.IsInside(centre)) return null;
            var cells = new List<Cell>();
            var wasFrozen = new List<bool>();
            foreach (var cell in SquareAround(centre))
            {
                if (Board.IsEmpty(cell)) continue;
                cells.Add(cell);
                wasFrozen.Add(Board.IsFrozen(cell));
            }
            if (cells.Count == 0) return null;

            if (!Timed) UndoStep = Snapshot();
            // A full pocket paused the count; using one starts it again from here (the next one 200 points later).
            if (Puffballs >= GameConfig.PuffballMax) NextPuffballAt = Score + GameConfig.PuffballEvery;
            Puffballs--;
            foreach (var cell in cells) Board.Remove(cell);
            var result = new BurstResult { Centre = centre, Cells = cells, WasFrozen = wasFrozen };
            result.GameOver = EndIfNoMoves();
            return result;
        }

        /// <summary>The cells of the 3×3 square around <paramref name="centre"/> that are on the board.</summary>
        public IEnumerable<Cell> SquareAround(Cell centre)
        {
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                var cell = new Cell(centre.Column + dc, centre.Row + dr);
                if (Board.IsInside(cell)) yield return cell;
            }
        }

        /// <summary>L16: where aiming starts: the cell whose 3×3 square holds the most balls (a full square preferred).</summary>
        public Cell BestBurstCell()
        {
            Cell best = new Cell(Board.Size / 2, Board.Size / 2);
            int bestValue = -1;
            for (int row = 0; row < Board.Size; row++)
            for (int column = 0; column < Board.Size; column++)
            {
                var cell = new Cell(column, row);
                int balls = 0, size = 0;
                foreach (var c in SquareAround(cell)) { size++; if (!Board.IsEmpty(c)) balls++; }
                int value = balls * 10 + (size == 9 ? 1 : 0);
                if (value > bestValue) { bestValue = value; best = cell; }
            }
            return best;
        }

        // The end of a move or a burst: no ball can move → game over, unless a puffball can rescue it (Stuck).
        bool EndIfNoMoves()
        {
            if (IsOver || !Board.NoMovesLeft() || Puffballs > 0) return false;
            EndNow();
            return true;
        }

        UndoSnapshot Snapshot() => new UndoSnapshot(Board.Clone(), Score, Preview, Puffballs, NextPuffballAt); // Preview is immutable

        float AddTime(int lines)
        {
            if (!Timed) return 0f;
            float added = GameConfig.TimeBonus * lines;
            TimeLeft += added;
            return added;
        }

        int Clear(List<Line> lines, int points, bool byMove)
        {
            var cells = LineDetector.CellsToClear(lines);
            foreach (var cell in cells) Board.Remove(cell);
            Score += points;
            EarnPuffballs();
            OnLinesCleared?.Invoke(lines, cells, points, byMove);
            return points;
        }

        // L16: one puffball per PuffballEvery points; with a full pocket nothing is earned and the count waits.
        void EarnPuffballs()
        {
            while (Puffballs < GameConfig.PuffballMax && Score >= NextPuffballAt)
            {
                NextPuffballAt += GameConfig.PuffballEvery;
                Puffballs++;
            }
        }
    }
}
