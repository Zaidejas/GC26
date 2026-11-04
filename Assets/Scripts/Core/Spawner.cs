using System;
using System.Collections.Generic;
using System.Linq;

namespace Lines.Core
{
    /// <summary>
    /// The next balls shown to the player: 3 colors and the cells where they will appear.
    /// Cells can be fewer than 3 when the board is nearly full. Immutable, so Undo can keep it as is.
    /// </summary>
    public sealed class Preview
    {
        public IReadOnlyList<BallColor> Colors { get; }
        public IReadOnlyList<Cell> Cells { get; }

        public Preview(IEnumerable<BallColor> colors, IEnumerable<Cell> cells)
        {
            Colors = colors.ToList();
            Cells = cells.ToList();
        }
    }

    /// <summary>
    /// Places the starting balls, chooses the next balls in advance (the preview) and spawns them.
    /// Same behavior as makePreview() / spawn() in docs/prototype/lines-prototype.html.
    /// </summary>
    public class Spawner
    {
        public GameRandom Random { get; }

        public Spawner(GameRandom random)
        {
            Random = random ?? throw new ArgumentNullException(nameof(random));
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
        /// Colors can repeat, as in the classic game.
        /// </summary>
        public Preview MakePreview(Board board)
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
            return new Preview(colors, cells);
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
                board.Place(cell, preview.Colors[k]);
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
