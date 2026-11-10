using System;
using System.Collections.Generic;
using System.Linq;

namespace Lines.Core
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
            };
            var undo = game.UndoStep;
            if (undo != null)
            {
                save.hasUndo = true;
                save.undoBoard = Cells(undo.Board);
                save.undoScore = undo.Score;
                save.undoPreviewColors = undo.Preview.Colors.Select(c => (int)c).ToArray();
                save.undoPreviewCells = undo.Preview.Cells.Select(Index).ToArray();
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
                var gameBoard = ReadBoard(board);
                var preview = ReadPreview(previewColors, previewCells);
                if (gameBoard == null || preview == null || score < 0) return null;
                if (!isOver && preview.Cells.Any(c => !gameBoard.IsEmpty(c))) return null;

                UndoSnapshot undo = null;
                if (hasUndo && !isOver)
                {
                    var undoGameBoard = ReadBoard(undoBoard);
                    var undoPreview = ReadPreview(undoPreviewColors, undoPreviewCells);
                    if (undoGameBoard == null || undoPreview == null || undoScore < 0) return null;
                    undo = new UndoSnapshot(undoGameBoard, undoScore, undoPreview);
                }

                var spawner = new Spawner(GameRandom.FromState(unchecked((uint)randomState)));
                return new GameState(gameBoard, preview, score, spawner, isOver, undo);
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

        static bool IsColor(int value) => value >= 1 && value <= GameConfig.ColorCount;

        static Board ReadBoard(int[] cells)
        {
            if (cells == null || cells.Length != GameConfig.BoardSize * GameConfig.BoardSize) return null;
            var board = new Board();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == 0) continue;
                if (!IsColor(cells[i])) return null;
                board.Place(CellAt(i), (BallColor)cells[i]);
            }
            return board;
        }

        static Preview ReadPreview(int[] colors, int[] cells)
        {
            if (colors == null || cells == null) return null;
            if (colors.Length != GameConfig.BallsPerTurn || cells.Length > GameConfig.BallsPerTurn) return null;
            if (!colors.All(IsColor)) return null;
            if (cells.Any(i => i < 0 || i >= GameConfig.BoardSize * GameConfig.BoardSize)) return null;
            if (cells.Distinct().Count() != cells.Length) return null;
            return new Preview(colors.Select(c => (BallColor)c), cells.Select(CellAt));
        }
    }
}
