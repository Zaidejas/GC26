using System;

namespace GC26._2048
{
    public enum EndReason
    {
        None,
        OutOfMoves,
        BoardFull
    }

    /// <summary>One game: the board plus score, moves left, cheese on the field and the mouse. Plain C#.</summary>
    public class CheeseSession
    {
        readonly Random rng;

        public Board Board { get; } = new Board();
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        /// <summary>Cheese on the field right now: grows with every cheese made, drops by what the mouse steals.</summary>
        public int Cheeses => Board.Count(CheeseRules.CheeseLevel);
        public bool CheeseShown { get; private set; }
        /// <summary>Most valuable item seen this game (8 = cheese).</summary>
        public int BestLevel { get; private set; }
        public EndReason Ended { get; private set; }
        public bool IsOver => Ended != EndReason.None;
        /// <summary>Move limit this game started with (a game saved under an older limit keeps it).</summary>
        public int MoveLimit { get; private set; } = CheeseRules.MoveLimit;
        public int MovesMade => MoveLimit - MovesLeft;
        /// <summary>The mouse that comes after the first cheese and eats cheese on its runs.</summary>
        public CheeseMouse Mouse { get; } = new CheeseMouse();

        public CheeseSession(int seed)
        {
            rng = new Random(seed);
        }

        public void StartNew()
        {
            Board.Clear();
            Score = 0;
            MoveLimit = CheeseRules.MoveLimit;
            MovesLeft = MoveLimit;
            CheeseShown = false;
            BestLevel = 0;
            Ended = EndReason.None;
            Mouse.Reset();
            Spawn(1);
            Spawn(1);
        }

        public void Restore(int[] cells, int score, int movesLeft, bool cheeseShown, int bestLevel,
            int moveLimit = CheeseRules.MoveLimit)
        {
            Board.Load(cells);
            MoveLimit = Math.Max(moveLimit, movesLeft);
            Score = score;
            MovesLeft = movesLeft;
            CheeseShown = cheeseShown;
            BestLevel = bestLevel;
            Ended = EndReason.None;
            Mouse.Reset();
        }

        /// <summary>Plays one move. A move that changes nothing costs nothing and spawns nothing.</summary>
        public MoveResult Move(Direction dir)
        {
            if (IsOver) return new MoveResult();
            var result = Board.Move(dir);
            if (!result.Changed) return result;

            MovesLeft--;
            Score += result.Points;
            Note(result.TopLevel);
            if (result.Cheeses > 0 && !CheeseShown)
            {
                CheeseShown = true;
                result.FirstCheese = true;
            }

            result.Spawned = Spawn(0);
            Mouse.AfterMove(Board, rng, result); // may eat cheese, so before the end check
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
