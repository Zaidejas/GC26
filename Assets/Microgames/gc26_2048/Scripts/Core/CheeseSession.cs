using System;

namespace GC26._2048
{
    public enum EndReason
    {
        None,
        OutOfMoves,
        BoardFull
    }

    /// <summary>One game: the board plus score, moves left and cheeses in the hole. Plain C#.</summary>
    public class CheeseSession
    {
        readonly Random rng;

        public Board Board { get; } = new Board();
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        public int Cheeses { get; private set; }
        public bool CheeseShown { get; private set; }
        /// <summary>Most valuable item seen this game (8 = cheese).</summary>
        public int BestLevel { get; private set; }
        public EndReason Ended { get; private set; }
        public bool IsOver => Ended != EndReason.None;
        public int MovesMade => CheeseRules.MoveLimit - MovesLeft;

        public CheeseSession(int seed)
        {
            rng = new Random(seed);
        }

        public void StartNew()
        {
            Board.Clear();
            Score = 0;
            MovesLeft = CheeseRules.MoveLimit;
            Cheeses = 0;
            CheeseShown = false;
            BestLevel = 0;
            Ended = EndReason.None;
            Spawn(1);
            Spawn(1);
        }

        public void Restore(int[] cells, int score, int movesLeft, int cheeses, bool cheeseShown, int bestLevel)
        {
            Board.Load(cells);
            Score = score;
            MovesLeft = movesLeft;
            Cheeses = cheeses;
            CheeseShown = cheeseShown;
            BestLevel = bestLevel;
            Ended = EndReason.None;
        }

        /// <summary>Plays one move. A move that changes nothing costs nothing and spawns nothing.</summary>
        public MoveResult Move(Direction dir)
        {
            if (IsOver) return new MoveResult();
            var result = Board.Move(dir);
            if (!result.Changed) return result;

            MovesLeft--;
            Score += result.Points;
            Cheeses += result.Cheeses;
            Note(result.TopLevel);
            if (result.Cheeses > 0 && !CheeseShown)
            {
                CheeseShown = true;
                result.FirstCheese = true;
            }

            result.Spawned = Spawn(0);
            if (MovesLeft <= 0) Ended = EndReason.OutOfMoves;
            else if (!Board.HasMoves()) Ended = EndReason.BoardFull;
            return result;
        }

        TileEvent? Spawn(int level)
        {
            var e = Board.SpawnRandom(rng, level);
            if (e.HasValue) Note(e.Value.Level);
            return e;
        }

        void Note(int level)
        {
            if (level > BestLevel) BestLevel = level;
        }
    }
}
