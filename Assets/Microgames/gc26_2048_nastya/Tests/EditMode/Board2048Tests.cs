using System;
using System.Linq;
using NUnit.Framework;

namespace GC26._2048Nastya.Tests
{
    public class Board2048Tests
    {
        // ---------- helpers ----------

        /// <summary>One row of a 4x4 board at the top, the rest empty.</summary>
        static Board2048 Row(params int[] row)
        {
            var values = new int[16];
            Array.Copy(row, values, 4);
            return Board2048.FromValues(4, values);
        }

        static int[] TopRow(Board2048 b) => b.ToValues().Take(b.Size).ToArray();

        static int[] Transpose(int[] v, int n)
        {
            var r = new int[v.Length];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    r[x * n + y] = v[y * n + x];
            return r;
        }

        static int[] MirrorX(int[] v, int n)
        {
            var r = new int[v.Length];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    r[y * n + (n - 1 - x)] = v[y * n + x];
            return r;
        }

        static int[] RandomValues(Random rng, int n)
        {
            var v = new int[n * n];
            for (int i = 0; i < v.Length; i++)
                v[i] = rng.Next(3) == 0 ? 0 : 1 << rng.Next(1, 5); // empty or 2..16
            return v;
        }

        static int TileCount(Board2048 b) => b.ToValues().Count(v => v != 0);

        // ---------- move rules (GDD 2.3) ----------

        [Test]
        public void Move_Left_FourEqual_MergesPairs()
        {
            var b = Row(2, 2, 2, 2);
            var r = b.Move(Direction2048.Left);
            Assert.AreEqual(new[] { 4, 4, 0, 0 }, TopRow(b));
            Assert.AreEqual(8, r.ScoreGained);
            Assert.AreEqual(2, r.Merges.Count);
            Assert.IsTrue(r.Moved);
        }

        [Test]
        public void Move_Left_ThreeEqual_MergesNearWall()
        {
            var b = Row(2, 2, 2, 0);
            b.Move(Direction2048.Left);
            Assert.AreEqual(new[] { 4, 2, 0, 0 }, TopRow(b));
        }

        [Test]
        public void Move_Right_ThreeEqual_MergesNearWall()
        {
            var b = Row(2, 2, 2, 0);
            b.Move(Direction2048.Right);
            Assert.AreEqual(new[] { 0, 0, 2, 4 }, TopRow(b));
        }

        [Test]
        public void Move_NoMergeTwiceInOneMove()
        {
            var b = Row(4, 4, 8, 0);
            var r = b.Move(Direction2048.Left);
            Assert.AreEqual(new[] { 8, 8, 0, 0 }, TopRow(b));
            Assert.AreEqual(8, r.ScoreGained);
        }

        [Test]
        public void Move_SlidesOverGaps()
        {
            var b = Row(0, 2, 0, 2);
            var r = b.Move(Direction2048.Left);
            Assert.AreEqual(new[] { 4, 0, 0, 0 }, TopRow(b));
            Assert.AreEqual(4, r.ScoreGained);
        }

        [Test]
        public void Move_Invalid_ReturnsMovedFalse()
        {
            var b = Row(2, 4, 2, 4);
            var before = b.ToValues();
            var r = b.Move(Direction2048.Left);
            Assert.IsFalse(r.Moved);
            Assert.AreEqual(0, r.ScoreGained);
            Assert.IsEmpty(r.Moves);
            Assert.IsEmpty(r.Merges);
            Assert.AreEqual(before, b.ToValues());
        }

        [Test]
        public void Move_AllDirections_Symmetric()
        {
            const int n = 4;
            var rng = new Random(12345);
            for (int iter = 0; iter < 500; iter++)
            {
                var v = RandomValues(rng, n);

                var left = Board2048.FromValues(n, v);
                var leftResult = left.Move(Direction2048.Left);

                // Right(B) = mirror(Left(mirror(B)))
                var right = Board2048.FromValues(n, v);
                var rightResult = right.Move(Direction2048.Right);
                var mirrored = Board2048.FromValues(n, MirrorX(v, n));
                var mirroredResult = mirrored.Move(Direction2048.Left);
                Assert.AreEqual(MirrorX(mirrored.ToValues(), n), right.ToValues(), "Right");
                Assert.AreEqual(mirroredResult.ScoreGained, rightResult.ScoreGained, "Right score");

                // Up(B) = transpose(Left(transpose(B)))
                var up = Board2048.FromValues(n, v);
                var upResult = up.Move(Direction2048.Up);
                var transposed = Board2048.FromValues(n, Transpose(v, n));
                var transposedResult = transposed.Move(Direction2048.Left);
                Assert.AreEqual(Transpose(transposed.ToValues(), n), up.ToValues(), "Up");
                Assert.AreEqual(transposedResult.ScoreGained, upResult.ScoreGained, "Up score");

                // Down(B) = transpose(Right(transpose(B)))
                var down = Board2048.FromValues(n, v);
                var downResult = down.Move(Direction2048.Down);
                var transposed2 = Board2048.FromValues(n, Transpose(v, n));
                var transposed2Result = transposed2.Move(Direction2048.Right);
                Assert.AreEqual(Transpose(transposed2.ToValues(), n), down.ToValues(), "Down");
                Assert.AreEqual(transposed2Result.ScoreGained, downResult.ScoreGained, "Down score");

                Assert.AreEqual(!v.SequenceEqual(left.ToValues()), leftResult.Moved, "Moved flag");
            }
        }

        [Test]
        public void Move_ReportsTileMovesAndMerges()
        {
            var b = Row(2, 2, 0, 4);
            int idA = b.Get(0, 0).Value.Id;
            int idB = b.Get(1, 0).Value.Id;
            int idC = b.Get(3, 0).Value.Id;

            var r = b.Move(Direction2048.Left);

            Assert.AreEqual(1, r.Merges.Count);
            var m = r.Merges[0];
            Assert.AreEqual(idA, m.SourceIdA);
            Assert.AreEqual(idB, m.SourceIdB);
            Assert.AreEqual(4, m.NewTile.Value);
            Assert.AreEqual((0, 0), (m.X, m.Y));
            Assert.AreNotEqual(idA, m.NewTile.Id);
            Assert.AreNotEqual(idB, m.NewTile.Id);
            Assert.AreEqual(m.NewTile.Id, b.Get(0, 0).Value.Id);

            // A stays in place (not reported), B slides 1 -> 0, C slides 3 -> 1.
            Assert.AreEqual(2, r.Moves.Count);
            Assert.IsFalse(r.Moves.Any(mv => mv.TileId == idA));
            var moveB = r.Moves.Single(mv => mv.TileId == idB);
            Assert.AreEqual((1, 0, 0, 0), (moveB.FromX, moveB.FromY, moveB.ToX, moveB.ToY));
            var moveC = r.Moves.Single(mv => mv.TileId == idC);
            Assert.AreEqual((3, 0, 1, 0), (moveC.FromX, moveC.FromY, moveC.ToX, moveC.ToY));
            Assert.AreEqual(idC, b.Get(1, 0).Value.Id);

            Assert.AreEqual(4, r.MaxValue);
        }

        [Test]
        public void Move_ReusedResult_IsCleared()
        {
            var b = Row(2, 2, 0, 0);
            var r = new MoveResult2048();
            b.Move(Direction2048.Left, r);
            Assert.AreEqual(1, r.Merges.Count);
            b.Move(Direction2048.Left, r); // [4 _ _ _] cannot move left
            Assert.IsFalse(r.Moved);
            Assert.IsEmpty(r.Merges);
            Assert.AreEqual(0, r.ScoreGained);
        }

        // ---------- lose condition ----------

        [Test]
        public void HasMoves_FullBoardWithPair_True()
        {
            var b = Board2048.FromValues(4, new[]
            {
                2, 4, 2, 4,
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 4,
            });
            Assert.IsTrue(b.HasMoves());
        }

        [Test]
        public void HasMoves_FullBoardNoPair_False()
        {
            var b = Board2048.FromValues(4, new[]
            {
                2, 4, 2, 4,
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 2,
            });
            Assert.IsFalse(b.HasMoves());
            foreach (Direction2048 d in Enum.GetValues(typeof(Direction2048)))
                Assert.IsFalse(b.Clone().Move(d).Moved, d.ToString());
        }

        [Test]
        public void HasMoves_EmptyCell_True()
        {
            Assert.IsTrue(Row(2, 4, 2, 4).HasMoves());
        }

        // ---------- board helpers ----------

        [Test]
        public void Clone_IsIndependentAndKeepsIds()
        {
            var b = Row(2, 2, 0, 0);
            var c = b.Clone();
            Assert.AreEqual(b.Get(0, 0).Value.Id, c.Get(0, 0).Value.Id);
            b.Move(Direction2048.Left);
            Assert.AreEqual(new[] { 2, 2, 0, 0 }, TopRow(c));
            // A new tile in the clone gets an Id that is not used on the clone.
            var placed = c.Place(3, 3, 2);
            Assert.AreNotEqual(c.Get(0, 0).Value.Id, placed.Id);
            Assert.AreNotEqual(c.Get(1, 0).Value.Id, placed.Id);
        }

        [Test]
        public void Place_InvalidValueOrOccupied_Throws()
        {
            var b = new Board2048();
            Assert.Throws<ArgumentException>(() => b.Place(0, 0, 3));
            Assert.Throws<ArgumentException>(() => b.Place(0, 0, 1));
            b.Place(0, 0, 2);
            Assert.Throws<InvalidOperationException>(() => b.Place(0, 0, 2));
        }

        [Test]
        public void ToValues_FromValues_RoundTrip()
        {
            var v = new[] { 0, 2, 0, 0, 4, 8, 0, 0, 0, 0, 0, 0, 2, 0, 0, 2048 };
            Assert.AreEqual(v, Board2048.FromValues(4, v).ToValues());
        }

        // ---------- spawner ----------

        [Test]
        public void Spawner_SeededDistribution()
        {
            var spawner = new TileSpawner2048(new Random(42));
            const int spawns = 10000;
            int fours = 0;

            var template = Board2048.FromValues(4, new[]
            {
                2, 0, 4, 0,
                0, 8, 0, 0,
                0, 0, 0, 16,
                0, 32, 0, 0,
            });

            for (int i = 0; i < spawns; i++)
            {
                var b = template.Clone();
                var s = spawner.Spawn(b);
                Assert.IsTrue(s.HasValue);
                Assert.IsTrue(template.IsEmpty(s.Value.X, s.Value.Y), "spawned into an occupied cell");
                Assert.AreEqual(s.Value.Tile.Id, b.Get(s.Value.X, s.Value.Y).Value.Id);
                if (s.Value.Tile.Value == 4) fours++;
                else Assert.AreEqual(2, s.Value.Tile.Value);
            }

            double ratio = fours / (double)spawns;
            Assert.That(ratio, Is.InRange(0.085, 0.115), $"share of 4s = {ratio:P1}");
        }

        [Test]
        public void Spawner_UsesEveryEmptyCell()
        {
            var spawner = new TileSpawner2048(new Random(7));
            var hits = new int[16];
            for (int i = 0; i < 2000; i++)
            {
                var s = spawner.Spawn(new Board2048()).Value;
                hits[s.Y * 4 + s.X]++;
            }
            Assert.IsTrue(hits.All(h => h > 0), "some cell is never chosen");
        }

        [Test]
        public void Spawner_FullBoard_ReturnsNull()
        {
            var b = Board2048.FromValues(4, Enumerable.Repeat(2, 16).ToArray());
            Assert.IsNull(new TileSpawner2048(new Random(1)).Spawn(b));
        }

        [Test]
        public void Spawner_SameSeed_SameResult()
        {
            var a = GameState2048.NewGame(new TileSpawner2048(new Random(99)));
            var b = GameState2048.NewGame(new TileSpawner2048(new Random(99)));
            Assert.AreEqual(a.Board.ToValues(), b.Board.ToValues());
        }

        // ---------- game state ----------

        [Test]
        public void NewGame_HasTwoTilesAndZeroScore()
        {
            var s = GameState2048.NewGame(new TileSpawner2048(new Random(3)), best: 500);
            Assert.AreEqual(2, TileCount(s.Board));
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(500, s.Best);
            Assert.AreEqual(0, s.Moves);
            Assert.IsFalse(s.IsOver);
            Assert.IsFalse(s.CanUndo);
        }

        [Test]
        public void TryMove_Valid_ScoresAndSpawnsOneTile()
        {
            var s = new GameState2048(Row(2, 2, 0, 4));
            var r = new MoveResult2048();
            bool ok = s.TryMove(Direction2048.Left, new TileSpawner2048(new Random(5)), r, out var spawned);

            Assert.IsTrue(ok);
            Assert.AreEqual(4, s.Score);
            Assert.AreEqual(4, s.Best);
            Assert.AreEqual(1, s.Moves);
            Assert.IsTrue(spawned.HasValue);
            Assert.AreEqual(3, TileCount(s.Board)); // 3 tiles - 1 merge + 1 spawn
            Assert.IsTrue(s.CanUndo);
        }

        [Test]
        public void TryMove_Invalid_ChangesNothing()
        {
            var s = new GameState2048(Row(2, 4, 2, 4), score: 10);
            var before = s.Board.ToValues();
            bool ok = s.TryMove(Direction2048.Left, new TileSpawner2048(new Random(5)), new MoveResult2048(), out var spawned);

            Assert.IsFalse(ok);
            Assert.IsNull(spawned);
            Assert.AreEqual(10, s.Score);
            Assert.AreEqual(0, s.Moves);
            Assert.AreEqual(before, s.Board.ToValues());
            Assert.IsFalse(s.CanUndo);
        }

        [Test]
        public void TryMove_LastMove_SetsIsOver()
        {
            // Left turns row 0 into [8 2 8 _]; the only empty cell (3, 0) gets a 4 (fourChance = 1).
            // The result [8 2 8 4] has no equal neighbors with the rows below → game over.
            var s = new GameState2048(Board2048.FromValues(4, new[]
            {
                4, 4, 2, 8,
                2, 4, 2, 8,
                4, 2, 4, 2,
                2, 4, 2, 4,
            }));
            Assert.IsFalse(s.IsOver);

            bool ok = s.TryMove(Direction2048.Left, new TileSpawner2048(new Random(1), 1f), new MoveResult2048(), out var spawned);
            Assert.IsTrue(ok);
            Assert.AreEqual((3, 0, 4), (spawned.Value.X, spawned.Value.Y, spawned.Value.Tile.Value));
            Assert.AreEqual(new[] { 8, 2, 8, 4 }, TopRow(s.Board));
            Assert.IsTrue(s.IsOver);
        }

        [Test]
        public void TryMove_WhenOver_ReturnsFalse()
        {
            var s = new GameState2048(Board2048.FromValues(4, new[]
            {
                2, 4, 2, 4,
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 2,
            }));
            Assert.IsTrue(s.IsOver);
            Assert.IsFalse(s.TryMove(Direction2048.Up, new TileSpawner2048(new Random(1)), new MoveResult2048(), out _));
        }

        [Test]
        public void Win_ShownOnlyOnce()
        {
            var s = new GameState2048(Row(1024, 1024, 0, 0));
            var r = new MoveResult2048();
            var spawner = new TileSpawner2048(new Random(2));

            Assert.IsTrue(s.TryMove(Direction2048.Left, spawner, r, out _));
            Assert.AreEqual(2048, r.MaxValue);
            Assert.IsTrue(s.ShouldShowWin(r));
            s.WonShown = true; // the player chose "Keep going"

            Assert.IsTrue(s.TryMove(Direction2048.Down, spawner, r, out _));
            Assert.IsFalse(s.ShouldShowWin(r));
        }

        [Test]
        public void Undo_RestoresBoardScoreAndMoves_OneStepOnly()
        {
            var s = new GameState2048(Row(2, 2, 0, 4), score: 100, best: 100);
            var before = s.Board.ToValues();
            s.TryMove(Direction2048.Left, new TileSpawner2048(new Random(8)), new MoveResult2048(), out _);
            Assert.AreEqual(104, s.Score);

            Assert.IsTrue(s.TryUndo());
            Assert.AreEqual(before, s.Board.ToValues());
            Assert.AreEqual(100, s.Score);
            Assert.AreEqual(0, s.Moves);
            Assert.AreEqual(104, s.Best, "best score is not undone");
            Assert.IsFalse(s.TryUndo(), "only one step");
        }

        [Test]
        public void Undo_NotAvailableInNewGame()
        {
            var s = GameState2048.NewGame(new TileSpawner2048(new Random(4)));
            Assert.IsFalse(s.TryUndo());
        }
    }
}
