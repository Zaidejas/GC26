using System.Linq;
using NUnit.Framework;

namespace GC26.Findepair.Tests
{
    public class FindepairRulesTests
    {
        [Test]
        public void Deck_HasEveryPairExactlyTwice()
        {
            var pairs = new[] { "a", "b", "c", "d", "e", "f" };
            for (int seed = 0; seed < 20; seed++)
            {
                var deck = FindepairRules.BuildDeck(pairs, new System.Random(seed));
                Assert.AreEqual(12, deck.Count);
                foreach (var p in pairs) Assert.AreEqual(2, deck.Count(x => x == p), p);
            }
        }

        [Test]
        public void Deck_IsShuffled()
        {
            var pairs = new[] { "a", "b", "c", "d", "e", "f" };
            var orders = Enumerable.Range(0, 10)
                .Select(s => string.Join("", FindepairRules.BuildDeck(pairs, new System.Random(s))))
                .Distinct().Count();
            Assert.Greater(orders, 1);
        }

        [TestCase(6, 3)]
        [TestCase(9, 3)]
        [TestCase(10, 2)]
        [TestCase(13, 2)]
        [TestCase(14, 1)]
        [TestCase(40, 1)]
        public void Stars_FollowTheGdd(int moves, int stars) => Assert.AreEqual(stars, FindepairRules.Stars(moves));

        [Test]
        public void Record_FewerMovesWins_TieGoesToTime()
        {
            Assert.IsTrue(FindepairRules.IsRecord(10, 50000, -1, -1), "first result is a record");
            Assert.IsTrue(FindepairRules.IsRecord(8, 90000, 9, 10000), "fewer moves");
            Assert.IsFalse(FindepairRules.IsRecord(10, 1000, 9, 90000), "more moves");
            Assert.IsTrue(FindepairRules.IsRecord(9, 9000, 9, 10000), "same moves, faster");
            Assert.IsFalse(FindepairRules.IsRecord(9, 10000, 9, 10000), "exact tie");
        }

        [Test]
        public void Grid_IsThreeByFourInPortrait_FourByThreeInLandscape()
        {
            Assert.AreEqual((3, 4), FindepairRules.Grid(12, true));
            Assert.AreEqual((4, 3), FindepairRules.Grid(12, false));
            Assert.AreEqual((3, 3), FindepairRules.Grid(8, true));
        }

        [Test]
        public void Time_IsMinutesAndSeconds()
        {
            Assert.AreEqual("00:00", FindepairRules.FormatTime(0));
            Assert.AreEqual("00:59", FindepairRules.FormatTime(59.9f));
            Assert.AreEqual("02:05", FindepairRules.FormatTime(125));
        }
    }
}
