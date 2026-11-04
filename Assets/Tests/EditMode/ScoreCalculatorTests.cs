using System;
using Lines.Core;
using NUnit.Framework;

namespace Lines.Tests
{
    public class ScoreCalculatorTests
    {
        [TestCase(5, 10)]
        [TestCase(6, 14)]
        [TestCase(7, 20)]
        [TestCase(8, 28)]
        [TestCase(9, 36)]
        public void PointsTable_MatchesGdd(int length, int points)
        {
            Assert.AreEqual(points, ScoreCalculator.PointsForLine(length));
        }

        [TestCase(4)]
        [TestCase(0)]
        [TestCase(10)]
        public void ImpossibleLength_Throws(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.PointsForLine(length));
        }

        [Test]
        public void Move_OneLine_NoMultiplier()
        {
            Assert.AreEqual(10, ScoreCalculator.ForMove(new[] { 5 }));
            Assert.AreEqual(36, ScoreCalculator.ForMove(new[] { 9 }));
        }

        [Test]
        public void Move_TwoLines_TimesTwo()
        {
            // (10 + 14) × 2
            Assert.AreEqual(48, ScoreCalculator.ForMove(new[] { 5, 6 }));
        }

        [Test]
        public void Move_ThreeLines_TimesThree()
        {
            // (10 + 10 + 20) × 3
            Assert.AreEqual(120, ScoreCalculator.ForMove(new[] { 5, 5, 7 }));
        }

        [Test]
        public void Spawn_NoMultiplier()
        {
            Assert.AreEqual(10, ScoreCalculator.ForSpawn(new[] { 5 }));
            Assert.AreEqual(24, ScoreCalculator.ForSpawn(new[] { 5, 6 }));
        }

        [Test]
        public void NoLines_ZeroPoints()
        {
            Assert.AreEqual(0, ScoreCalculator.ForMove(new int[0]));
            Assert.AreEqual(0, ScoreCalculator.ForSpawn(new int[0]));
        }

        [Test]
        public void CrossingLines_SharedBallCountsInBoth_GddExample()
        {
            // GDD: two crossing 5-lines → (10 + 10) × 2 = 40, although only 9 balls disappear.
            var board = TestBoards.FromRows(
                ".........",
                ".........",
                "....R....",
                "....R....",
                "..RRRRR..",
                "....R....",
                "....R....",
                ".........",
                ".........");
            var lines = LineDetector.FindLinesThrough(board, new Cell(4, 4));

            Assert.AreEqual(40, ScoreCalculator.ForMove(lines));
            Assert.AreEqual(9, LineDetector.CellsToClear(lines).Count);
        }

        [Test]
        public void SpawnedLines_FromDetector_NoMultiplier()
        {
            var board = TestBoards.FromRows(
                "GGGGG....",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "...PPPPPP");
            var lines = LineDetector.FindLinesThrough(board, new[] { new Cell(0, 0), new Cell(8, 8) });

            // 10 + 14, not × 2
            Assert.AreEqual(24, ScoreCalculator.ForSpawn(lines));
        }

        [Test]
        public void NullLines_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ScoreCalculator.ForMove((Line[])null));
        }
    }
}
