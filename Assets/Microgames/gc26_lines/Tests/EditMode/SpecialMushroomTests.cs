using System.Linq;
using GC26.Lines.Core;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>
    /// Tasks L14–L16 (GDD → Special mushrooms): the rainbow joker, frozen mushrooms, the puffball bomb and the
    /// no-moves ending. Boards: '*' a rainbow, lower-case letters frozen balls (see <see cref="TestBoards"/>).
    /// </summary>
    public class SpecialMushroomTests
    {
        static Cell C(int column, int row) => new Cell(column, row);
        static Preview Next(params Cell[] cells) => new Preview(new[] { BallColor.Yellow, BallColor.Cyan, BallColor.Orange }, cells);

        static Board Rows(params string[] rows) => TestBoards.FromRows(rows);
        static readonly string Empty = ".........";

        // ---- L14: the rainbow ----

        [Test]
        public void ARainbow_FitsAnyLine()
        {
            var board = Rows("GG*GG....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            var lines = LineDetector.FindLinesThrough(board, C(2, 0));
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(5, lines[0].Length);
            Assert.AreEqual(BallColor.Green, lines[0].Color, "the line takes its plain colour");
            Assert.AreEqual(1, LineDetector.FindLinesThrough(board, C(0, 0)).Count, "found from a plain ball too");
        }

        [Test]
        public void OneRainbow_CanFinishTwoLinesOfDifferentColours()
        {
            var board = Rows("RRRR*BBBB", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            var lines = LineDetector.FindLinesThrough(board, C(4, 0));
            Assert.AreEqual(2, lines.Count);
            Assert.IsTrue(lines.Any(l => l.Color == BallColor.Red && l.Length == 5));
            Assert.IsTrue(lines.Any(l => l.Color == BallColor.Blue && l.Length == 5));
            Assert.AreEqual(9, LineDetector.CellsToClear(lines).Count, "the rainbow is cleared once");
        }

        [Test]
        public void ARainbow_DoesntJoinTwoColoursIntoOneLine()
        {
            var board = Rows("RR*BB....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            Assert.AreEqual(0, LineDetector.FindLinesThrough(board, C(2, 0)).Count);
            board = Rows("**R**....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            Assert.AreEqual(1, LineDetector.FindLinesThrough(board, C(0, 0)).Count, "rainbows on both sides of one colour");
            board = Rows("*****....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            Assert.AreEqual(BallColor.Rainbow, LineDetector.FindLinesThrough(board, C(2, 0)).Single().Color, "rainbows alone");
        }

        [Test]
        public void AMoveFinishingTwoLinesWithARainbow_ScoresBothTimesTwo()
        {
            var board = Rows("RRRR.BBBB", "....*....", Empty, Empty, Empty, Empty, Empty, Empty, "........P");
            var game = new GameState(board, Next(C(0, 8), C(1, 8), C(2, 8)), 0, new Spawner(3));
            var result = game.TryMove(C(4, 1), C(4, 0));
            Assert.AreEqual(2, result.LinesFromMove.Count);
            Assert.AreEqual((10 + 10) * 2, result.MovePoints);
        }

        [Test]
        public void ANewGamesFirstNext_NeverHasARainbow_ButLaterOnesCan()
        {
            for (int seed = 0; seed < 500; seed++)
                Assert.IsFalse(GameState.NewGame(seed).Preview.Colors.Contains(BallColor.Rainbow), "seed " + seed);
            int rainbows = 0, made = 0;
            var spawner = new Spawner(11);
            for (int i = 0; i < 2000; i++)
            {
                var next = spawner.MakePreview(new Board());
                rainbows += next.Colors.Count(c => c == BallColor.Rainbow);
                made += next.Colors.Count;
            }
            Assert.That(rainbows, Is.InRange(made / GameConfig.RainbowOdds / 2, made / GameConfig.RainbowOdds * 2), "about 1 in 40");
        }

        [Test]
        public void TheSpecials_DontChangeThePlainColoursAndCells()
        {
            // The specials have their own random numbers: the same seed gives the same plain colours and cells as before.
            var a = new Spawner(5).MakePreview(new Board(), 0, rainbows: false);
            var spawner = new Spawner(5);
            var b = spawner.MakePreview(new Board(), 500, rainbows: false);
            CollectionAssert.AreEqual(a.Cells, b.Cells);
            for (int i = 0; i < a.Colors.Count; i++)
                if (!b.IsFrozen(i)) Assert.AreEqual(a.Colors[i], b.Colors[i]);
        }

        // ---- L15: frozen mushrooms ----

        [Test]
        public void Ice_OnlyFrom100Points_AndNeverMoreThan4()
        {
            var spawner = new Spawner(21);
            for (int i = 0; i < 300; i++) Assert.IsFalse(spawner.MakePreview(new Board(), 99).Frozen.Contains(true), "none before 100 points");
            int frozen = 0, made = 0;
            for (int i = 0; i < 2000; i++)
            {
                var next = spawner.MakePreview(new Board(), 100);
                frozen += next.Frozen.Count(f => f);
                made += next.Colors.Count;
                for (int k = 0; k < next.Colors.Count; k++)
                    if (next.Colors[k] == BallColor.Rainbow) Assert.IsFalse(next.IsFrozen(k), "a rainbow is never frozen");
            }
            Assert.That(frozen, Is.InRange(made / GameConfig.IceOdds / 2, made / GameConfig.IceOdds * 2), "about 1 in 20");
            var four = Rows("rrrr.....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty);
            for (int i = 0; i < 500; i++) Assert.IsFalse(spawner.MakePreview(four, 300).Frozen.Contains(true), "4 frozen already: no more");
        }

        [Test]
        public void AFrozenBall_CantMove_ButCountsForItsColour()
        {
            var board = Rows("RRrR.....", "....R....", Empty, Empty, Empty, Empty, Empty, Empty, "........P");
            Assert.IsTrue(board.IsFrozen(C(2, 0)));
            var game = new GameState(board, Next(C(0, 8), C(1, 8), C(2, 8)), 0, new Spawner(3));
            Assert.Throws<System.ArgumentException>(() => game.TryMove(C(2, 0), C(2, 1)));
            var result = game.TryMove(C(4, 1), C(4, 0));
            Assert.AreEqual(1, result.LinesFromMove.Count, "the line goes through the frozen one");
            Assert.IsTrue(game.Board.IsEmpty(C(2, 0)));
            Assert.IsFalse(game.Board.IsFrozen(C(2, 0)), "the ice went with it");
        }

        [Test]
        public void FrozenNext_ArriveFrozen()
        {
            var board = Rows(Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty, "R.......B");
            var next = new Preview(new[] { BallColor.Yellow, BallColor.Cyan, BallColor.Orange }, new[] { C(0, 0), C(4, 4), C(8, 0) }, new[] { false, true, false });
            var game = new GameState(board, next, 0, new Spawner(3));
            var result = game.TryMove(C(0, 8), C(1, 8));
            Assert.IsTrue(game.Board.IsFrozen(C(4, 4)));
            Assert.IsFalse(game.Board.IsFrozen(C(0, 0)));
            CollectionAssert.AreEqual(new[] { false, true, false }, result.SpawnedFrozen);
        }

        [Test]
        public void FreeCellsWalledInByIce_EndTheGame_WithNoMovesLeft()
        {
            // The only free cell (4, 4) is walled in by frozen balls; the others can't reach it.
            var board = Rows("RGBYCOPRG", "GBYCOPRGB", "BYCOPRGBY", "YCOPgRGBY", "COPr.bGBY", "OPRGyBYCO", "PRGBYCOPR", "RGBYCOPRG", "GBYCOPRGB");
            Assert.IsTrue(board.NoMovesLeft());
            Assert.IsFalse(board.IsFull);
            var notStuck = Rows("RGBYCOPRG", "GBYCOPRGB", "BYCOPRGBY", "YCOPGRGBY", "COPr.bGBY", "OPRGyBYCO", "PRGBYCOPR", "RGBYCOPRG", "GBYCOPRGB");
            Assert.IsFalse(notStuck.NoMovesLeft(), "a plain ball next to the free cell can move");
        }

        // ---- L16: the puffball ----

        static GameState NearLine(int score, int puffballs = 0, int nextAt = 0) =>
            new GameState(Rows("RRRR.....", "....R....", Empty, Empty, "..BB.....", "..BB.....", Empty, Empty, "........P"),
                Next(C(0, 8), C(1, 8), C(2, 8)), score, new Spawner(3), puffballs: puffballs, nextPuffballAt: nextAt);

        [Test]
        public void APuffball_Every200Points_KeepUpTo2()
        {
            var game = NearLine(195);
            Assert.AreEqual(200, game.NextPuffballAt);
            var result = game.TryMove(C(4, 1), C(4, 0));
            Assert.AreEqual(1, result.PuffballsEarned);
            Assert.AreEqual(1, game.Puffballs);
            Assert.AreEqual(400, game.NextPuffballAt);
            Assert.AreEqual(205, game.Score);
        }

        [Test]
        public void AFullPocket_PausesTheCount_AndUsingOneStartsItAgainFromHere()
        {
            var game = NearLine(395, puffballs: 2, nextAt: 400);
            var result = game.TryMove(C(4, 1), C(4, 0));
            Assert.AreEqual(0, result.PuffballsEarned, "full: nothing more");
            Assert.AreEqual(2, game.Puffballs);
            Assert.AreEqual(1f, game.PuffballProgress, "the ring stays full");
            Assert.IsNotNull(game.Burst(C(3, 5)));
            Assert.AreEqual(1, game.Puffballs);
            Assert.AreEqual(game.Score + GameConfig.PuffballEvery, game.NextPuffballAt, "the next one 200 points from now");
        }

        [Test]
        public void ABurst_Clears3x3_NoPoints_NoNewBalls_AndUndoTakesItBack()
        {
            var board = Rows("RRrR.....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, "........P");
            var game = new GameState(board, Next(C(0, 8), C(1, 8), C(2, 8)), 50, new Spawner(3), puffballs: 1);
            var nextBefore = game.Preview;
            var burst = game.Burst(C(2, 1));
            Assert.AreEqual(3, burst.Cells.Count, "(1,0), (2,0) frozen, (3,0)");
            Assert.IsTrue(burst.WasFrozen[burst.Cells.ToList().IndexOf(C(2, 0))]);
            Assert.IsTrue(game.Board.IsEmpty(C(1, 0)) && game.Board.IsEmpty(C(2, 0)) && game.Board.IsEmpty(C(3, 0)));
            Assert.IsFalse(game.Board.IsEmpty(C(0, 0)), "outside the square");
            Assert.AreEqual(50, game.Score, "no points");
            Assert.AreSame(nextBefore, game.Preview, "not a move: no new balls, NEXT stays");
            Assert.AreEqual(0, game.Puffballs);
            Assert.IsTrue(game.Undo());
            Assert.AreEqual(1, game.Puffballs, "Undo gives the puffball back");
            Assert.IsTrue(game.Board.IsFrozen(C(2, 0)));
        }

        [Test]
        public void ABurst_NeedsAPuffball_AndABallInTheSquare()
        {
            var game = NearLine(0);
            Assert.IsNull(game.Burst(C(4, 4)), "no puffball");
            game = NearLine(0, puffballs: 1);
            Assert.IsNull(game.Burst(C(7, 2)), "an empty square");
            Assert.AreEqual(1, game.Puffballs);
        }

        [Test]
        public void AimingStarts_OnTheBusiestSpot()
        {
            var game = NearLine(0, puffballs: 1);
            Assert.AreEqual(C(2, 4), game.BestBurstCell(), "the first spot whose square holds all 4 blue (a full square preferred)");
        }

        [Test]
        public void NoMovesWithAPuffball_IsStuckNotOver_EndGameEndsIt()
        {
            var full = Rows("RGBYCOPRG", "GBYCOPRGB", "BYCOPRGBY", "YCOPGRGBY", "COPRGBGBY", "OPRGYBYCO", "PRGBYCOPR", "RGBYCOPRG", "GBYCOPR..");
            var game = new GameState(full, new Preview(new[] { BallColor.Red, BallColor.Red, BallColor.Red }, new[] { C(7, 8), C(8, 8) }),
                10, new Spawner(3), puffballs: 1);
            var result = game.TryMove(C(6, 8), C(7, 8));
            Assert.IsFalse(game.IsOver, "a puffball can rescue it");
            Assert.IsTrue(game.Stuck);
            Assert.IsFalse(result.GameOver);
            game.EndGame();
            Assert.IsTrue(game.IsOver);
            Assert.IsFalse(game.EndedWithNoMoves, "a full board: the usual Final score");
        }

        // ---- Saving ----

        [Test]
        public void Save_KeepsIceRainbowsPuffballsAndTutorials()
        {
            var board = Rows("RRr*.....", Empty, Empty, Empty, Empty, Empty, Empty, Empty, "........P");
            var next = new Preview(new[] { BallColor.Rainbow, BallColor.Cyan, BallColor.Orange }, new[] { C(0, 8), C(1, 8), C(2, 8) }, new[] { false, true, false });
            var game = new GameState(board, next, 250, new Spawner(9), puffballs: 2, nextPuffballAt: 400);
            game.TutorialsShown.Add("ice");
            var copy = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(GameSave.From(game))).ToGameState();
            Assert.IsNotNull(copy);
            Assert.IsTrue(copy.Board.IsFrozen(C(2, 0)));
            Assert.AreEqual(BallColor.Rainbow, copy.Board.Get(C(3, 0)));
            Assert.AreEqual(BallColor.Rainbow, copy.Preview.Colors[0]);
            Assert.IsTrue(copy.Preview.IsFrozen(1));
            Assert.AreEqual(2, copy.Puffballs);
            CollectionAssert.Contains(copy.TutorialsShown, "ice");
            Assert.AreEqual(game.Spawner.Specials.State, copy.Spawner.Specials.State, "the specials' random numbers carry on");
        }

        [Test]
        public void AnOldSave_LoadsWithNoSpecials()
        {
            var old = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(GameSave.From(NearLine(230))));
            old.frozen = null; old.previewFrozen = null; old.puffballs = 0; old.nextPuffballAt = 0; old.hasSpecialsState = false; old.tutorials = null;
            var game = old.ToGameState();
            Assert.IsNotNull(game);
            Assert.AreEqual(0, game.Board.FrozenCount);
            Assert.AreEqual(400, game.NextPuffballAt, "the next 200 after the score");
        }
    }
}
