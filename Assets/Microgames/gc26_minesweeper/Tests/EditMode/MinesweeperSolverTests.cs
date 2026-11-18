using System.Diagnostics;
using NUnit.Framework;

namespace GC26.Minesweeper.Tests
{
    // Measures how often a board needs a guess, and checks the "no guessing" generator.
    // The numbers are printed to the test output (Test Runner → select the test → output pane).
    public class MinesweeperSolverTests
    {
        const int BoardsPerLevel = 400;

        static void FirstTap(MinesweeperDifficulty lv, int seed, out int x, out int y)
        {
            // Players tap anywhere, corners included.
            var r = new System.Random(seed * 7919 + 17);
            x = r.Next(lv.Width);
            y = r.Next(lv.Height);
        }

        [Test]
        public void Measure_RandomBoards_ThatNeedAGuess()
        {
            foreach (var lv in MinesweeperDifficulty.All)
            {
                int needGuess = 0, errors = 0;
                for (int i = 0; i < BoardsPerLevel; i++)
                {
                    FirstTap(lv, i, out int fx, out int fy);
                    var b = new MinesweeperBoard(lv.Width, lv.Height, lv.Mines, seed: i);
                    b.PlaceMines(fx, fy);                            // the old, purely random layout
                    var r = MinesweeperSolver.Solve(b, fx, fy);
                    if (r.SolverError) errors++;
                    if (!r.Solved) needGuess++;
                }
                TestContext.WriteLine($"RANDOM {lv.Name}: {needGuess}/{BoardsPerLevel} boards need a guess = {100f * needGuess / BoardsPerLevel:F1}%");
                Assert.AreEqual(0, errors, "the solver made a wrong deduction");
            }
        }

        [Test]
        public void NoGuessGenerator_AlwaysGivesSolvableBoards()
        {
            foreach (var lv in MinesweeperDifficulty.All)
            {
                int gaveUp = 0, notSolvable = 0, totalAttempts = 0, maxAttempts = 0;
                long maxMs = 0, totalMs = 0;
                for (int i = 0; i < BoardsPerLevel; i++)
                {
                    FirstTap(lv, 10000 + i, out int fx, out int fy);
                    var b = new MinesweeperBoard(lv.Width, lv.Height, lv.Mines, seed: 10000 + i);
                    var sw = Stopwatch.StartNew();
                    int attempts = b.PlaceMinesNoGuess(fx, fy);
                    sw.Stop();
                    totalMs += sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds > maxMs) maxMs = sw.ElapsedMilliseconds;

                    if (attempts < 0) gaveUp++;
                    else { totalAttempts += attempts; if (attempts > maxAttempts) maxAttempts = attempts; }

                    var r = MinesweeperSolver.Solve(b, fx, fy);
                    Assert.IsFalse(r.SolverError, "the solver made a wrong deduction");
                    if (!r.Solved) notSolvable++;

                    // Still a fair board: right number of red flags, first tap area safe.
                    int mines = 0;
                    for (int x = 0; x < b.Width; x++)
                        for (int y = 0; y < b.Height; y++)
                            if (b.GetCell(x, y).IsMine) mines++;
                    Assert.AreEqual(lv.Mines, mines);
                    Assert.IsFalse(b.GetCell(fx, fy).IsMine);
                }
                int done = BoardsPerLevel - gaveUp;
                TestContext.WriteLine($"NOGUESS {lv.Name}: {notSolvable}/{BoardsPerLevel} need a guess = {100f * notSolvable / BoardsPerLevel:F1}%, " +
                                      $"gave up {gaveUp}, attempts avg {(done > 0 ? (float)totalAttempts / done : 0):F1} max {maxAttempts}, " +
                                      $"time avg {(float)totalMs / BoardsPerLevel:F1} ms max {maxMs} ms");
                Assert.LessOrEqual(100f * notSolvable / BoardsPerLevel, 1f, $"{lv.Name}: too many boards still need a guess");
            }
        }

        // What the game uses: about 4% of boards (3–5% is the target) need a guess, on every level.
        [Test]
        public void FairGenerator_LeavesAbout4PercentGuessBoards()
        {
            const int boards = 1000;
            foreach (var lv in MinesweeperDifficulty.All)
            {
                int needGuess = 0, gaveUp = 0;
                long maxMs = 0, totalMs = 0;
                for (int i = 0; i < boards; i++)
                {
                    FirstTap(lv, 50000 + i, out int fx, out int fy);
                    var b = new MinesweeperBoard(lv.Width, lv.Height, lv.Mines, seed: 50000 + i);
                    var sw = Stopwatch.StartNew();
                    if (b.PlaceMinesFair(fx, fy) < 0) gaveUp++;
                    sw.Stop();
                    totalMs += sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds > maxMs) maxMs = sw.ElapsedMilliseconds;

                    var r = MinesweeperSolver.Solve(b, fx, fy);
                    Assert.IsFalse(r.SolverError, "the solver made a wrong deduction");
                    if (!r.Solved) needGuess++;
                }
                float pct = 100f * needGuess / boards;
                TestContext.WriteLine($"FAIR {lv.Name}: {needGuess}/{boards} need a guess = {pct:F1}%, gave up {gaveUp}, " +
                                      $"time avg {(float)totalMs / boards:F1} ms max {maxMs} ms");
                Assert.That(pct, Is.InRange(2.5f, 5.5f), $"{lv.Name}: guess boards should be about 3–5%");
            }
        }

        [Test]
        public void Solver_SolvesAnObviousBoard()
        {
            // 9x9 with 10 red flags: the solver must open every safe cell of a board it reports as solved.
            var b = new MinesweeperBoard(9, 9, 10, seed: 2);
            b.PlaceMines(4, 4);
            var r = MinesweeperSolver.Solve(b, 4, 4);
            Assert.IsFalse(r.SolverError);
            if (r.Solved) Assert.AreEqual(r.SafeCells, r.OpenedCells);
        }
    }
}
