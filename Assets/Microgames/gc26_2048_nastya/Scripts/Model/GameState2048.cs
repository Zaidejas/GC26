namespace GC26._2048Nastya
{
    /// <summary>The board, score and moves before the last valid move (one-step undo, GDD 8.2).</summary>
    public sealed class UndoSnapshot2048
    {
        public readonly Board2048 Board;
        public readonly int Score;
        public readonly int Moves;

        public UndoSnapshot2048(Board2048 board, int score, int moves)
        {
            Board = board;
            Score = score;
            Moves = moves;
        }
    }

    /// <summary>One game: board, score, best score and flags (TDD 4.5). Pure C#.</summary>
    public sealed class GameState2048
    {
        public const int WinValue = 2048;
        public const int StartTiles = 2;

        public Board2048 Board;
        public int Score;
        public int Best;
        public int Moves;

        /// <summary>The win overlay was already shown in this game; never show it again.</summary>
        public bool WonShown;

        /// <summary>No move is possible: game over.</summary>
        public bool IsOver;

        /// <summary>Null when there is nothing to undo (new game, after a resume, right after an undo).</summary>
        public UndoSnapshot2048 Undo;

        public bool CanUndo => Undo != null;

        public GameState2048(Board2048 board, int score = 0, int best = 0, int moves = 0, bool wonShown = false)
        {
            Board = board;
            Score = score;
            Best = best > score ? best : score;
            Moves = moves;
            WonShown = wonShown;
            IsOver = !board.HasMoves();
        }

        /// <summary>A fresh game with <see cref="StartTiles"/> random tiles.</summary>
        public static GameState2048 NewGame(TileSpawner2048 spawner, int best = 0, int size = Board2048.DefaultSize)
        {
            var board = new Board2048(size);
            for (int i = 0; i < StartTiles; i++)
                spawner.Spawn(board);
            return new GameState2048(board, 0, best);
        }

        /// <summary>
        /// Plays one move (TDD 5.2, steps 1-4): move, score, spawn, game-over check.
        /// Returns false for an invalid move; then nothing changes and <paramref name="spawned"/> is null.
        /// </summary>
        public bool TryMove(Direction2048 dir, TileSpawner2048 spawner, MoveResult2048 result, out TileSpawn2048? spawned)
        {
            spawned = null;
            if (IsOver)
            {
                result.Clear();
                return false;
            }

            var before = Board.Clone();
            Board.Move(dir, result);
            if (!result.Moved) return false;

            Undo = new UndoSnapshot2048(before, Score, Moves);
            Score += result.ScoreGained;
            if (Score > Best) Best = Score;
            Moves++;
            spawned = spawner.Spawn(Board);
            IsOver = !Board.HasMoves();
            return true;
        }

        /// <summary>True the first time a <see cref="WinValue"/> tile appears in this game.</summary>
        public bool ShouldShowWin(MoveResult2048 result) => !WonShown && result.Moved && result.MaxValue >= WinValue;

        /// <summary>Restores the state before the last valid move. The best score is kept.</summary>
        public bool TryUndo()
        {
            if (Undo == null) return false;
            Board = Undo.Board;
            Score = Undo.Score;
            Moves = Undo.Moves;
            Undo = null;
            IsOver = !Board.HasMoves();
            return true;
        }
    }
}
