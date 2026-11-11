using System;
using System.Collections.Generic;

namespace BlockDrop.Core
{
    public enum GameState { Playing, Paused, GameOver }

    public enum GameMode { Marathon, Battle }

    /// <summary>What one line clear earned, for the score popups and effects.</summary>
    public readonly struct LineClear
    {
        public readonly IReadOnlyList<int> Rows;
        public readonly int Points;
        public readonly int Combo;
        public readonly float Multiplier;

        public LineClear(IReadOnlyList<int> rows, int points, int combo, float multiplier)
        {
            Rows = rows;
            Points = points;
            Combo = combo;
            Multiplier = multiplier;
        }
    }

    /// <summary>All game rules. Has no Unity dependency: the view feeds it time and commands.</summary>
    public sealed class Game
    {
        public const int PreviewCount = 3;
        const int LinesPerLevel = 10;
        const float LockDelay = 0.5f;
        const int MaxLockResets = 15;
        const float SoftDropInterval = 0.25f;
        const int SoftDropRows = 2;
        const float ComboStep = 0.5f;
        const float MaxComboMultiplier = 4f;
        const int SpecialPointsPerCell = 10;
        const int BombRadius = 2;


        /// <summary>The cell of a piece that carries its special. Rotations keep the cell order.</summary>
        public const int SpecialCellIndex = 1;

        static readonly int[] LineScores = { 0, 100, 300, 500, 800 };

        // Tried in order when a rotation is blocked.
        static readonly Cell[] Kicks =
        {
            new Cell(0, 0), new Cell(-1, 0), new Cell(1, 0), new Cell(0, 1),
            new Cell(-2, 0), new Cell(2, 0), new Cell(0, 2), new Cell(0, -1),
        };

        readonly PieceBag bag;
        readonly SpecialDeck specialDeck;
        readonly List<PieceType> upcoming = new List<PieceType>();
        readonly List<SpecialKind> upcomingSpecials = new List<SpecialKind>();

        float gravityTimer;
        float lockTimer;
        int lockResets;
        int lowestY;

        public Board Board { get; } = new Board();
        public GameState State { get; private set; } = GameState.Playing;
        public GameMode Mode { get; }

        public bool IsFinished { get { return State == GameState.GameOver; } }
        public PieceType CurrentType { get; private set; }
        public int CurrentRotation { get; private set; }
        public int CurrentX { get; private set; }
        public int CurrentY { get; private set; }
        public SpecialKind CurrentSpecial { get; private set; }
        public PieceType? HeldType { get; private set; }
        public SpecialKind HeldSpecial { get; private set; }
        public bool CanHold { get; private set; } = true;
        public int Score { get; private set; }
        public int Lines { get; private set; }
        public int Level { get { return 1 + Lines / LinesPerLevel; } }

        /// <summary>Pieces in a row that cleared at least one line; any other lock resets it.</summary>
        public int Combo { get; private set; }

        /// <summary>Row indices (before removal) of the rows just cleared.</summary>
        public event Action<IReadOnlyList<int>> RowsCleared;
        public event Action<LineClear> LinesCleared;
        public event Action<PieceType> PieceLocked;
        /// <summary>Raised before the piece moves, with its start and landing rows.</summary>
        public event Action<int, int> HardDropped;
        public event Action<int> LevelChanged;
        public event Action<SpecialResult> SpecialTriggered;
        public event Action GameEnded;

        /// <summary>The first clear scores x1, each further clear in a row adds x0.5, up to x4.</summary>
        public static float ComboMultiplier(int combo)
        {
            return Math.Min(MaxComboMultiplier, 1f + ComboStep * Math.Max(0, combo - 1));
        }

        /// <param name="specials">False deals only ordinary pieces.</param>
        public Game(int seed, bool specials = true, GameMode mode = GameMode.Marathon)
        {
            Mode = mode;
            bag = new PieceBag(seed);
            specialDeck = specials ? new SpecialDeck(seed) : null;
            for (int i = 0; i < PreviewCount; i++)
                DealUpcoming();
            SpawnNext();
        }

        public PieceType GetUpcoming(int index)
        {
            return upcoming[index];
        }

        public SpecialKind GetUpcomingSpecial(int index)
        {
            return upcomingSpecials[index];
        }

        /// <summary>Makes the falling piece special. For tests and the -specialCycle test aid.</summary>
        public void ForceCurrentSpecial(SpecialKind kind)
        {
            CurrentSpecial = kind;
        }

        void DealUpcoming()
        {
            upcoming.Add(bag.Take());
            upcomingSpecials.Add(specialDeck != null ? specialDeck.Next() : SpecialKind.None);
        }

        /// <summary>Seconds per row at the current level.</summary>
        public float GravityInterval
        {
            get
            {
                float interval = 0.85f;
                for (int i = 1; i < Level; i++)
                    interval *= 0.82f;
                return Math.Max(0.05f, interval);
            }
        }

        public int GhostY
        {
            get
            {
                int y = CurrentY;
                while (Board.Fits(CurrentType, CurrentRotation, CurrentX, y - 1))
                    y--;
                return y;
            }
        }

        /// <summary>Score from outside the board rules, such as hitting the bot. Ignored once the game is over.</summary>
        public void AddBonusScore(int points)
        {
            if (State == GameState.GameOver || points <= 0)
                return;
            Score += points;
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
                State = GameState.Paused;
            else if (State == GameState.Paused)
                State = GameState.Playing;
        }

        public void Tick(float deltaTime, bool softDrop)
        {
            if (State != GameState.Playing)
                return;

            // At high levels normal gravity outruns the soft drop, so it only applies while it is faster.
            bool useSoftDrop = softDrop && SoftDropInterval / SoftDropRows < GravityInterval;
            float interval = useSoftDrop ? SoftDropInterval : GravityInterval;
            int rowsPerStep = useSoftDrop ? SoftDropRows : 1;

            gravityTimer += deltaTime;
            // Time saved up under normal gravity must not turn into several steps at once.
            if (useSoftDrop)
                gravityTimer = Math.Min(gravityTimer, interval);

            while (gravityTimer >= interval)
            {
                gravityTimer -= interval;
                for (int row = 0; row < rowsPerStep; row++)
                {
                    if (!Board.Fits(CurrentType, CurrentRotation, CurrentX, CurrentY - 1))
                    {
                        gravityTimer = 0f;
                        break;
                    }
                    DropOneRow();
                    if (softDrop)
                        Score += 1;
                }
            }

            if (Board.Fits(CurrentType, CurrentRotation, CurrentX, CurrentY - 1))
                return;

            lockTimer += deltaTime;
            if (lockTimer >= LockDelay)
                LockCurrent();
        }

        public bool Move(int direction)
        {
            if (State != GameState.Playing || !Board.Fits(CurrentType, CurrentRotation, CurrentX + direction, CurrentY))
                return false;
            CurrentX += direction;
            OnPieceAdjusted();
            return true;
        }

        /// <param name="direction">+1 for clockwise, -1 for counter-clockwise.</param>
        public bool Rotate(int direction)
        {
            if (State != GameState.Playing)
                return false;

            int rotation = PieceShapes.Mod(CurrentRotation + direction, PieceShapes.RotationCount);
            foreach (Cell kick in Kicks)
            {
                if (!Board.Fits(CurrentType, rotation, CurrentX + kick.X, CurrentY + kick.Y))
                    continue;
                CurrentRotation = rotation;
                CurrentX += kick.X;
                CurrentY += kick.Y;
                OnPieceAdjusted();
                return true;
            }
            return false;
        }

        public void HardDrop()
        {
            if (State != GameState.Playing)
                return;
            int target = GhostY;
            Score += (CurrentY - target) * 2;
            HardDropped?.Invoke(CurrentY, target);
            CurrentY = target;
            LockCurrent();
        }

        public bool Hold()
        {
            if (State != GameState.Playing || !CanHold)
                return false;

            PieceType current = CurrentType;
            SpecialKind currentSpecial = CurrentSpecial;
            if (HeldType.HasValue)
                Spawn(HeldType.Value, HeldSpecial);
            else
                SpawnNext();
            HeldType = current;
            HeldSpecial = currentSpecial;
            CanHold = false;
            return true;
        }

        void DropOneRow()
        {
            CurrentY--;
            if (CurrentY < lowestY)
            {
                lowestY = CurrentY;
                lockTimer = 0f;
                lockResets = 0;
            }
        }

        void OnPieceAdjusted()
        {
            if (lockResets >= MaxLockResets)
                return;
            lockTimer = 0f;
            lockResets++;
        }

        void LockCurrent()
        {
            PieceType locked = CurrentType;
            SpecialKind special = CurrentSpecial;
            Cell specialCell = PieceShapes.Get(CurrentType, CurrentRotation)[SpecialCellIndex];
            int specialX = CurrentX + specialCell.X;
            int specialY = CurrentY + specialCell.Y;
            Board.Place(CurrentType, CurrentRotation, CurrentX, CurrentY);
            PieceLocked?.Invoke(locked);

            List<int> cleared = Board.ClearFullRows();
            if (cleared.Count > 0)
            {
                int levelBefore = Level;
                Combo++;
                float multiplier = ComboMultiplier(Combo);
                int points = (int)Math.Round(LineScores[cleared.Count] * Level * multiplier);
                Score += points;
                Lines += cleared.Count;
                RowsCleared?.Invoke(cleared);
                LinesCleared?.Invoke(new LineClear(cleared, points, Combo, multiplier));
                if (Level != levelBefore)
                    LevelChanged?.Invoke(Level);
            }
            else if (special == SpecialKind.None)
            {
                // A special piece keeps the chain: using one should never feel like a penalty.
                Combo = 0;
            }

            if (special != SpecialKind.None)
            {
                // Lines clear first, so the piece's own row can count; the blast then follows the settled stack.
                int rowsBelow = 0;
                foreach (int row in cleared)
                {
                    if (row < specialY)
                        rowsBelow++;
                }
                ResolveSpecial(special, specialX, specialY - rowsBelow);
            }

            CanHold = true;
            SpawnNext();
        }

        void ResolveSpecial(SpecialKind kind, int x, int y)
        {
            var removed = new List<RemovedCell>();
            switch (kind)
            {
                case SpecialKind.Bomb:
                    Explode(x, y, removed);
                    break;
                case SpecialKind.Lightning:
                    RemoveColumn(x, removed);
                    break;
            }

            int points = removed.Count * SpecialPointsPerCell * Level;
            Score += points;
            SpecialTriggered?.Invoke(new SpecialResult(kind, x, y, points, removed));
        }

        // Removes the square around the bomb; what stood above the crater then falls into it.
        void Explode(int centerX, int centerY, List<RemovedCell> removed)
        {
            int bottom = Math.Max(0, centerY - BombRadius);
            int top = Math.Min(Board.Height - 1, centerY + BombRadius);
            for (int x = Math.Max(0, centerX - BombRadius); x <= Math.Min(Board.Width - 1, centerX + BombRadius); x++)
            {
                for (int y = bottom; y <= top; y++)
                {
                    int value = Board.Get(x, y);
                    if (value == Board.Empty)
                        continue;
                    removed.Add(new RemovedCell(x, y, value));
                    Board.Set(x, y, Board.Empty);
                }

                int write = bottom;
                for (int y = bottom; y < Board.Height; y++)
                {
                    int value = Board.Get(x, y);
                    if (value == Board.Empty)
                        continue;
                    Board.Set(x, y, Board.Empty);
                    Board.Set(x, write, value);
                    write++;
                }
            }
        }

        void RemoveColumn(int x, List<RemovedCell> removed)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                int value = Board.Get(x, y);
                if (value == Board.Empty)
                    continue;
                removed.Add(new RemovedCell(x, y, value));
                Board.Set(x, y, Board.Empty);
            }
        }

        void SpawnNext()
        {
            PieceType next = upcoming[0];
            SpecialKind nextSpecial = upcomingSpecials[0];
            upcoming.RemoveAt(0);
            upcomingSpecials.RemoveAt(0);
            DealUpcoming();
            Spawn(next, nextSpecial);
        }

        void Spawn(PieceType type, SpecialKind special)
        {
            int topCell = 0;
            foreach (Cell c in PieceShapes.Get(type, 0))
                topCell = Math.Max(topCell, c.Y);

            CurrentType = type;
            CurrentSpecial = special;
            CurrentRotation = 0;
            CurrentX = (Board.Width - PieceShapes.BoxSize(type)) / 2;
            CurrentY = Board.VisibleHeight - 1 - topCell;
            lowestY = CurrentY;
            gravityTimer = 0f;
            lockTimer = 0f;
            lockResets = 0;

            if (!Board.Fits(type, 0, CurrentX, CurrentY))
            {
                State = GameState.GameOver;
                GameEnded?.Invoke();
            }
        }
    }
}
