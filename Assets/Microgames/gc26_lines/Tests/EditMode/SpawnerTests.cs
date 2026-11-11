using System;
using System.Collections.Generic;
using System.Linq;
using GC26.Lines.Core;
using NUnit.Framework;

namespace GC26.Lines.Tests
{
    public class SpawnerTests
    {
        // ---- GameRandom ----

        [Test]
        public void GameRandom_SameSeed_SameNumbers()
        {
            var a = new GameRandom(123);
            var b = new GameRandom(123);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        [Test]
        public void GameRandom_DifferentSeeds_DifferentNumbers()
        {
            var r1 = new GameRandom(1);
            var r2 = new GameRandom(2);
            var first = Enumerable.Range(0, 20).Select(_ => r1.Next(1000)).ToList();
            var second = Enumerable.Range(0, 20).Select(_ => r2.Next(1000)).ToList();
            CollectionAssert.AreNotEqual(first, second);
        }

        [Test]
        public void GameRandom_StaysInRange_AndCoversAllValues()
        {
            var random = new GameRandom(7);
            var counts = new int[7];
            for (int i = 0; i < 7000; i++)
            {
                int n = random.Next(7);
                Assert.That(n, Is.InRange(0, 6));
                counts[n]++;
            }
            // Roughly even: each value should appear about 1000 times.
            foreach (var c in counts) Assert.That(c, Is.InRange(800, 1200));
        }

        [Test]
        public void GameRandom_ContinuesFromSavedState()
        {
            var original = new GameRandom(99);
            for (int i = 0; i < 5; i++) original.Next(100);
            var restored = GameRandom.FromState(original.State);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(original.Next(100), restored.Next(100));
        }

        [Test]
        public void GameRandom_SeedZero_Works()
        {
            var random = new GameRandom(0);
            var values = Enumerable.Range(0, 10).Select(_ => random.Next(1000)).ToList();
            Assert.Greater(values.Distinct().Count(), 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(0));
        }

        // ---- Start balls ----

        [Test]
        public void StartBalls_ThreeBallsOnDifferentCells()
        {
            var board = new Board();
            var placed = new Spawner(1).PlaceStartBalls(board);

            Assert.AreEqual(3, placed.Count);
            CollectionAssert.AllItemsAreUnique(placed);
            Assert.AreEqual(78, board.EmptyCount);
            foreach (var cell in placed)
                CollectionAssert.Contains(BallColors.All, board.Get(cell));
        }

        [Test]
        public void SameSeed_SameStartAndPreview()
        {
            var (boardA, previewA) = StartGame(2026);
            var (boardB, previewB) = StartGame(2026);

            for (int row = 0; row < 9; row++)
            for (int column = 0; column < 9; column++)
                Assert.AreEqual(boardA.Get(new Cell(column, row)), boardB.Get(new Cell(column, row)));
            CollectionAssert.AreEqual(previewA.Colors, previewB.Colors);
            CollectionAssert.AreEqual(previewA.Cells, previewB.Cells);
        }

        [Test]
        public void DifferentSeeds_DifferentGames()
        {
            var starts = Enumerable.Range(1, 5)
                .Select(seed => string.Join(",", StartGame(seed).board.EmptyCells()))
                .ToList();
            Assert.Greater(starts.Distinct().Count(), 1);
        }

        // ---- Preview ----

        [Test]
        public void Preview_ThreeColorsAndThreeDifferentEmptyCells()
        {
            var (board, preview) = StartGame(5);

            Assert.AreEqual(3, preview.Colors.Count);
            Assert.IsFalse(preview.Colors.Contains(BallColor.None));
            Assert.AreEqual(3, preview.Cells.Count);
            CollectionAssert.AllItemsAreUnique(preview.Cells);
            foreach (var cell in preview.Cells)
                Assert.IsTrue(board.IsEmpty(cell));
        }

        [Test]
        public void Preview_NearlyFullBoard_FewerCells()
        {
            var board = FullBoardExcept(new Cell(0, 0), new Cell(8, 8));
            var preview = new Spawner(3).MakePreview(board);

            Assert.AreEqual(3, preview.Colors.Count, "The next colors are always 3");
            CollectionAssert.AreEquivalent(new[] { new Cell(0, 0), new Cell(8, 8) }, preview.Cells);
        }

        [Test]
        public void Preview_FullBoard_NoCells()
        {
            var board = FullBoardExcept();
            Assert.IsEmpty(new Spawner(3).MakePreview(board).Cells);
        }

        // ---- Spawn ----

        [Test]
        public void Spawn_PlacesPreviewColorsOnPreviewCells()
        {
            var (board, preview) = StartGame(11);
            var spawner = new Spawner(11);

            var placed = spawner.Spawn(board, preview);

            CollectionAssert.AreEqual(preview.Cells, placed);
            for (int k = 0; k < 3; k++)
                Assert.AreEqual(preview.Colors[k], board.Get(preview.Cells[k]));
            Assert.AreEqual(75, board.EmptyCount);
        }

        [Test]
        public void Spawn_PreviewCellTaken_BallGoesToAnotherEmptyCell()
        {
            var (board, preview) = StartGame(12);
            var taken = preview.Cells[1];
            board.Place(taken, BallColor.Red); // as if the player moved a ball onto the dot
            var emptyBefore = new HashSet<Cell>(board.EmptyCells());

            var placed = new Spawner(12).Spawn(board, preview);

            Assert.AreEqual(3, placed.Count);
            Assert.AreEqual(preview.Cells[0], placed[0]);
            Assert.AreNotEqual(taken, placed[1]);
            Assert.IsTrue(emptyBefore.Contains(placed[1]), "Moved to a cell that was empty");
            Assert.AreEqual(preview.Colors[1], board.Get(placed[1]), "The ball keeps its color");
            Assert.AreEqual(BallColor.Red, board.Get(taken), "The player's ball stays");
            CollectionAssert.AllItemsAreUnique(placed);
        }

        [Test]
        public void Spawn_AllPreviewCellsTaken_AllMoved()
        {
            var (board, preview) = StartGame(13);
            foreach (var cell in preview.Cells) board.Place(cell, BallColor.Blue);
            int emptyBefore = board.EmptyCount;

            var placed = new Spawner(13).Spawn(board, preview);

            Assert.AreEqual(3, placed.Count);
            CollectionAssert.IsEmpty(placed.Intersect(preview.Cells));
            Assert.AreEqual(emptyBefore - 3, board.EmptyCount);
        }

        [Test]
        public void Spawn_FewerThanThreeFreeCells_FillsBoardAndStops()
        {
            var board = FullBoardExcept(new Cell(4, 4), new Cell(5, 5));
            var spawner = new Spawner(14);
            var preview = spawner.MakePreview(board);

            var placed = spawner.Spawn(board, preview);

            Assert.AreEqual(2, placed.Count);
            Assert.IsTrue(board.IsFull);
        }

        [Test]
        public void Spawn_OneFreeCell_ButPreviewCellsTaken_StillFillsIt()
        {
            var board = FullBoardExcept(new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0));
            var spawner = new Spawner(15);
            var preview = spawner.MakePreview(board);
            // The player fills all but one free cell before the spawn.
            foreach (var cell in board.EmptyCells().Take(3)) board.Place(cell, BallColor.Green);

            var placed = spawner.Spawn(board, preview);

            Assert.AreEqual(1, placed.Count);
            Assert.IsTrue(board.IsFull);
        }

        [Test]
        public void Spawn_FullGame_NeverPlacesOnOccupiedCells()
        {
            // Keep spawning until the board is full; every ball must land on a free cell.
            var board = new Board();
            var spawner = new Spawner(16);
            spawner.PlaceStartBalls(board);
            int rounds = 0;
            while (!board.IsFull)
            {
                var preview = spawner.MakePreview(board);
                int emptyBefore = board.EmptyCount;
                var placed = spawner.Spawn(board, preview);
                Assert.AreEqual(Math.Min(3, emptyBefore), placed.Count);
                Assert.AreEqual(emptyBefore - placed.Count, board.EmptyCount);
                Assert.Less(++rounds, 100);
            }
        }

        // ---- Helpers ----

        static (Board board, Preview preview) StartGame(int seed)
        {
            var board = new Board();
            var spawner = new Spawner(seed);
            spawner.PlaceStartBalls(board);
            return (board, spawner.MakePreview(board));
        }

        static Board FullBoardExcept(params Cell[] free)
        {
            var board = new Board();
            int i = 0;
            foreach (var cell in board.EmptyCells())
                if (!free.Contains(cell))
                    board.Place(cell, BallColors.All[i++ % BallColors.All.Length]);
            return board;
        }
    }
}
