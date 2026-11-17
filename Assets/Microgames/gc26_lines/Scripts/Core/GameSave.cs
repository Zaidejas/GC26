using System;
using System.Collections.Generic;
using System.Linq;

namespace GC26.Lines.Core
{
    /// <summary>
    /// Everything needed to continue a game later (GDD → Saving the game in progress): the board, score,
    /// preview, the Undo step and the random generator's state (so the coming pieces stay the same).
    /// Plain fields only, so it can be written as JSON. (Older saves also list a creature set; it is
    /// ignored now that every game uses the same 7 mushrooms.)
    /// Cells are stored as numbers 0–80 (row × 9 + column); colors as numbers 1–7.
    /// </summary>
    [Serializable]
    public class GameSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int[] board;
        public int score;
        public int[] previewColors;
        public int[] previewCells;
        public bool isOver;

        public bool hasUndo;
        public int[] undoBoard;
        public int undoScore;
        public int[] undoPreviewColors;
        public int[] undoPreviewCells;

        /// <summary>The random generator's state (a 32-bit number, kept in a long for JSON).</summary>
        public long randomState;

        /// <summary>
        /// The game is over and its score waits on the game-over pop-up (not yet on the leaderboard).
        /// If the app closes before Save, the next launch adds it under the pre-filled name.
        /// </summary>
        public bool hasPendingScore;

        /// <summary>Timed mode (task L09): the game has a clock, and the seconds left on it. Older saves are Classic.</summary>
        public bool timed;
        public float timeLeft;
        /// <summary>
        /// Special mushrooms (tasks L14–L17): which balls are frozen (on the board and in NEXT, also for the Undo step),
        /// the puffballs in stock and the score of the next one, the tutorials this game has shown, and the
        /// specials' own random numbers. Older saves have none of these: a game with no specials (colors 1–7).
        /// </summary>
        public bool[] frozen;
        public bool[] previewFrozen;
        public bool[] undoFrozen;
        public bool[] undoPreviewFrozen;
        public int puffballs;
        public int nextPuffballAt;
        public int undoPuffballs;
        public int undoNextPuffballAt;
        public bool hasSpecialsState;
        public long specialsState;
        public string[] tutorials;

        /// <summary>Captures <paramref name="game"/>.</summary>
        public static GameSave From(GameState game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var save = new GameSave
            {
                board = Cells(game.Board),
                score = game.Score,
                previewColors = game.Preview.Colors.Select(c => (int)c).ToArray(),
                previewCells = game.Preview.Cells.Select(Index).ToArray(),
                isOver = game.IsOver,
                randomState = game.Spawner.Random.State,
                timed = game.Timed,
                timeLeft = game.TimeLeft,
                frozen = Frozen(game.Board),
                previewFrozen = game.Preview.Frozen.ToArray(),
                puffballs = game.Puffballs,
                nextPuffballAt = game.NextPuffballAt,
                hasSpecialsState = true,
                specialsState = game.Spawner.Specials.State,
                tutorials = game.TutorialsShown.OrderBy(t => t).ToArray(),
            };
            var undo = game.UndoStep;
            if (undo != null)
            {
                save.hasUndo = true;
                save.undoBoard = Cells(undo.Board);
                save.undoScore = undo.Score;
                save.undoPreviewColors = undo.Preview.Colors.Select(c => (int)c).ToArray();
                save.undoPreviewCells = undo.Preview.Cells.Select(Index).ToArray();
                save.undoFrozen = Frozen(undo.Board);
                save.undoPreviewFrozen = undo.Preview.Frozen.ToArray();
                save.undoPuffballs = undo.Puffballs;
                save.undoNextPuffballAt = undo.NextPuffballAt;
            }
            return save;
        }

        /// <summary>
        /// Rebuilds the game. Returns null when the data is damaged or from an unknown version,
        /// so the caller can simply start a new game instead.
        /// </summary>
        public GameState ToGameState()
        {
            try
            {
                if (version != CurrentVersion) return null;
                var gameBoard = ReadBoard(board, frozen);
                var preview = ReadPreview(previewColors, previewCells, previewFrozen);
                if (gameBoard == null || preview == null || score < 0) return null;
                if (!isOver && preview.Cells.Any(c => !gameBoard.IsEmpty(c))) return null;

                UndoSnapshot undo = null;
                if (hasUndo && !isOver)
                {
                    var undoGameBoard = ReadBoard(undoBoard, undoFrozen);
                    var undoPreview = ReadPreview(undoPreviewColors, undoPreviewCells, undoPreviewFrozen);
                    if (undoGameBoard == null || undoPreview == null || undoScore < 0) return null;
                    undo = new UndoSnapshot(undoGameBoard, undoScore, undoPreview, undoPuffballs, undoNextPuffballAt);
                }

                var random = GameRandom.FromState(unchecked((uint)randomState));
                var spawner = new Spawner(random, hasSpecialsState ? GameRandom.FromState(unchecked((uint)specialsState)) : null);
                if (timed && (float.IsNaN(timeLeft) || timeLeft < 0f)) return null;
                if (puffballs < 0 || puffballs > GameConfig.PuffballMax) return null;
                var game = new GameState(gameBoard, preview, score, spawner, isOver, undo, timed, timeLeft, puffballs, nextPuffballAt);
                if (tutorials != null) foreach (var t in tutorials) if (!string.IsNullOrEmpty(t)) game.TutorialsShown.Add(t);
                return game;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static int Index(Cell cell) => cell.Row * GameConfig.BoardSize + cell.Column;
        static Cell CellAt(int index) => new Cell(index % GameConfig.BoardSize, index / GameConfig.BoardSize);

        static int[] Cells(Board board)
        {
            var cells = new int[board.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = (int)board.Get(CellAt(i));
            return cells;
        }

        // The 7 plain colors and the rainbow (L14).
        static bool IsColor(int value) => value >= 1 && value <= (int)BallColor.Rainbow;

        static bool[] Frozen(Board board)
        {
            var result = new bool[board.CellCount];
            for (int i = 0; i < result.Length; i++) result[i] = board.IsFrozen(CellAt(i));
            return result;
        }

        static Board ReadBoard(int[] cells, bool[] frozenCells)
        {
            if (cells == null || cells.Length != GameConfig.BoardSize * GameConfig.BoardSize) return null;
            if (frozenCells != null && frozenCells.Length != 0 && frozenCells.Length != cells.Length) return null;
            var board = new Board();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == 0) continue;
                if (!IsColor(cells[i])) return null;
                bool ice = frozenCells != null && frozenCells.Length == cells.Length && frozenCells[i];
                board.Place(CellAt(i), (BallColor)cells[i], ice);
            }
            return board;
        }

        static Preview ReadPreview(int[] colors, int[] cells, bool[] frozenNext)
        {
            if (colors == null || cells == null) return null;
            if (colors.Length != GameConfig.BallsPerTurn || cells.Length > GameConfig.BallsPerTurn) return null;
            if (!colors.All(IsColor)) return null;
            if (cells.Any(i => i < 0 || i >= GameConfig.BoardSize * GameConfig.BoardSize)) return null;
            if (cells.Distinct().Count() != cells.Length) return null;
            return new Preview(colors.Select(c => (BallColor)c), cells.Select(CellAt), frozenNext);
        }
    }
}
