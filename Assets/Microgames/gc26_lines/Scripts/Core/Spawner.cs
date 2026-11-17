using System;
using System.Collections.Generic;
using System.Linq;

namespace GC26.Lines.Core
{
    /// <summary>
    /// The next balls shown to the player: 3 colors and the cells where they will appear.
    /// Cells can be fewer than 3 when the board is nearly full. Immutable, so Undo can keep it as is.
    /// </summary>
    public sealed class Preview
    {
        public IReadOnlyList<BallColor> Colors { get; }
        public IReadOnlyList<Cell> Cells { get; }
        /// <summary>L15: which of the next balls arrive frozen (same order as <see cref="Colors"/>).</summary>
        public IReadOnlyList<bool> Frozen { get; }

        public Preview(IEnumerable<BallColor> colors, IEnumerable<Cell> cells, IEnumerable<bool> frozen = null)
        {
            Colors = colors.ToList();
            Cells = cells.ToList();
            var ice = frozen?.ToList() ?? new List<bool>();
            while (ice.Count < Colors.Count) ice.Add(false);
            for (int i = 0; i < ice.Count; i++) if (Colors[i] == BallColor.Rainbow) ice[i] = false; // a rainbow is never frozen
            Frozen = ice;
        }

        /// <summary>Whether the <paramref name="index"/>-th next ball arrives frozen.</summary>
        public bool IsFrozen(int index) => index >= 0 && index < Frozen.Count && Frozen[index];
    }

    /// <summary>
    /// Places the starting balls, chooses the next balls in advance (the preview) and spawns them.
    /// Same behavior as makePreview() / spawn() in docs/prototype/lines-prototype.html.
    /// </summary>
    public class Spawner
    {
        public GameRandom Random { get; }
        /// <summary>
        /// Its own random numbers for the special mushrooms (rainbow, ice), so the plain colours and cells come out
        /// exactly as before for the same seed. Saved with the game.
        /// </summary>
        public GameRandom Specials { get; }

        public Spawner(GameRandom random, GameRandom specials = null)
        {
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Specials = specials ?? GameRandom.FromState(random.State ^ 0x9E3779B9u);
        }

        public Spawner(int seed) : this(new GameRandom(seed)) { }

        public BallColor RandomColor() => BallColors.All[Random.Next(BallColors.All.Length)];

        /// <summary>Puts the starting balls (3) on random empty cells. Returns where they went.</summary>
        public List<Cell> PlaceStartBalls(Board board, int count = GameConfig.StartBalls)
        {
            var placed = new List<Cell>();
            for (int i = 0; i < count; i++)
            {
                if (!TryPickEmptyCell(board, out var cell)) break;
                board.Place(cell, RandomColor());
                placed.Add(cell);
            }
            return placed;
        }

        /// <summary>
        /// Chooses the next 3 colors and up to 3 different empty cells for them.
        /// Colors can repeat, as in the classic game. L14: each one is a rainbow 1 time in
        /// <see cref="GameConfig.RainbowOdds"/> (not when <paramref name="rainbows"/> is false: a new game's first NEXT).
        /// L15: from <see cref="GameConfig.IceFrom"/> points (<paramref name="score"/>) each plain one arrives frozen
        /// 1 time in <see cref="GameConfig.IceOdds"/>, while fewer than <see cref="GameConfig.IceMax"/> are frozen.
        /// </summary>
        public Preview MakePreview(Board board, int score = 0, bool rainbows = true)
        {
            var empty = board.EmptyCells();
            var cells = new List<Cell>();
            while (cells.Count < GameConfig.BallsPerTurn && empty.Count > 0)
            {
                int i = Random.Next(empty.Count);
                cells.Add(empty[i]);
                empty.RemoveAt(i);
            }
            var colors = new List<BallColor>();
            for (int i = 0; i < GameConfig.BallsPerTurn; i++)
                colors.Add(RandomColor());

            var frozen = new List<bool>();
            int frozenNow = board.FrozenCount;
            for (int i = 0; i < colors.Count; i++)
            {
                if (rainbows && Specials.Next(GameConfig.RainbowOdds) == 0) colors[i] = BallColor.Rainbow;
                bool ice = colors[i] != BallColor.Rainbow && score >= GameConfig.IceFrom && frozenNow < GameConfig.IceMax
                    && Specials.Next(GameConfig.IceOdds) == 0;
                if (ice) frozenNow++;
                frozen.Add(ice);
            }
            return new Preview(colors, cells, frozen);
        }

        /// <summary>
        /// Places the preview balls. A ball whose cell is taken (the player moved onto it)
        /// goes to another random empty cell. Stops early when the board is full.
        /// Returns the cells where balls were placed.
        /// </summary>
        public List<Cell> Spawn(Board board, Preview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            var placed = new List<Cell>();
            for (int k = 0; k < preview.Colors.Count; k++)
            {
                Cell cell;
                if (k < preview.Cells.Count && board.IsEmpty(preview.Cells[k]))
                    cell = preview.Cells[k];
                else if (!TryPickEmptyCell(board, out cell))
                    break;
                board.Place(cell, preview.Colors[k], preview.IsFrozen(k));
                placed.Add(cell);
            }
            return placed;
        }

        bool TryPickEmptyCell(Board board, out Cell cell)
        {
            var empty = board.EmptyCells();
            if (empty.Count == 0)
            {
                cell = default;
                return false;
            }
            cell = empty[Random.Next(empty.Count)];
            return true;
        }
    }
}
