using NUnit.Framework;

namespace GC26.Minesweeper.Tests
{
    // Edit Mode tests for the rules (Window → General → Test Runner, no Play needed).
    public class MinesweeperBoardTests
    {
        static int CountMines(MinesweeperBoard b)
        {
            int n = 0;
            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                    if (b.GetCell(x, y).IsMine) n++;
            return n;
        }

        [Test]
        public void PlaceMines_PlacesExactCount_AndKeepsFirstTapSafe()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 42);
            b.PlaceMines(4, 4);
            Assert.AreEqual(10, CountMines(b));
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    Assert.IsFalse(b.GetCell(4 + dx, 4 + dy).IsMine);
        }

        [Test]
        public void FirstTapInCorner_IsSafe()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 7);
            b.PlaceMines(0, 0);
            Assert.IsFalse(b.GetCell(0, 0).IsMine);
            Assert.IsFalse(b.GetCell(1, 1).IsMine);
        }

        [Test]
        public void Expert_Gets99Mines()
        {
            var lv = MinesweeperDifficulty.Expert;
            var b = new MinesweeperBoard(lv.Width, lv.Height, lv.Mines, seed: 3);
            b.PlaceMines(8, 15);
            Assert.AreEqual(16, b.Width);
            Assert.AreEqual(30, b.Height);
            Assert.AreEqual(99, CountMines(b));
        }

        [Test]
        public void Numbers_MatchNeighbourMines()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 11);
            b.PlaceMines(4, 4);
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                {
                    int expected = 0;
                    foreach (var n in b.Neighbours(x, y))
                        if (b.GetCell(n.x, n.y).IsMine) expected++;
                    Assert.AreEqual(expected, b.GetCell(x, y).AdjacentMines, $"cell {x},{y}");
                }
        }

        [Test]
        public void FirstTap_FloodFillOpensArea_AndStopsAtNumbers()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 5);
            b.PlaceMines(4, 4);
            var opened = b.Reveal(4, 4);
            Assert.Greater(opened.Count, 1, "the first tap opens a 0, so an area opens");
            foreach (var p in opened)
            {
                var c = b.GetCell(p.x, p.y);
                Assert.IsFalse(c.IsMine);
                // A cell behind a number is only opened if some opened 0 touches it.
                if (c.AdjacentMines > 0) continue;
                foreach (var n in b.Neighbours(p.x, p.y))
                    Assert.IsTrue(b.GetCell(n.x, n.y).IsRevealed, "every neighbour of an opened 0 is opened");
            }
        }

        [Test]
        public void FlaggedCell_CannotBeRevealed()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 1);
            b.PlaceMines(0, 0);
            Assert.IsTrue(b.ToggleFlag(8, 8));
            Assert.IsEmpty(b.Reveal(8, 8));
            Assert.AreEqual(1, b.FlagCount);
        }

        [Test]
        public void OpenCell_CannotBeFlagged()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 1);
            b.PlaceMines(4, 4);
            b.Reveal(4, 4);
            Assert.IsFalse(b.ToggleFlag(4, 4));
            Assert.AreEqual(0, b.FlagCount);
        }

        [Test]
        public void OpeningAllSafeCells_Wins()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 9);
            b.PlaceMines(4, 4);
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                    if (!b.GetCell(x, y).IsMine) b.Reveal(x, y);
            Assert.IsTrue(b.IsWon);
        }

        [Test]
        public void Chord_DoesNothing_WhenFlagCountIsWrong()
        {
            var b = new MinesweeperBoard(9, 9, 10, seed: 13);
            b.PlaceMines(4, 4);
            b.Reveal(4, 4);
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                {
                    var c = b.GetCell(x, y);
                    if (c.IsRevealed && c.AdjacentMines > 0)
                    {
                        Assert.IsEmpty(b.Chord(x, y), "no flags placed, so chording opens nothing");
                        return;
                    }
                }
            Assert.Inconclusive("no opened number on this board");
        }

        [Test]
        public void LossLines_SkipsEmptyAndCommentLines()
        {
            string[] lines = MinesweeperLossLines.Parse("# comment\r\nFirst line\r\n\r\n  Second line  \n#another\n");
            CollectionAssert.AreEqual(new[] { "First line", "Second line" }, lines);
        }

        [Test]
        public void LossLines_EmptyList_GivesEmptyLine()
        {
            Assert.AreEqual("", MinesweeperLossLines.PickRandom(MinesweeperLossLines.Parse(""), new System.Random(1)));
        }
    }
}
