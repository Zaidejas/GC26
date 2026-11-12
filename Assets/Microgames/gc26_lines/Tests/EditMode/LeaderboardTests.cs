using GC26.Lines.Core;
using NUnit.Framework;

namespace GC26.Lines.Tests
{
    public class LeaderboardTests
    {
        Leaderboard board;

        [SetUp]
        public void SetUp() => board = new Leaderboard();

        [Test]
        public void Scores_AreOrderedBestFirst()
        {
            board.Add(50, "A", 1);
            board.Add(120, "B", 2);
            board.Add(80, "C", 3);
            CollectionAssert.AreEqual(new[] { 120, 80, 50 }, board.entries.ConvertAll(e => e.score));
        }

        [Test]
        public void Add_ReturnsThePlace()
        {
            Assert.AreEqual(0, board.Add(50, "A", 1));
            Assert.AreEqual(0, board.Add(120, "B", 2));
            Assert.AreEqual(2, board.Add(10, "C", 3));
        }

        [Test]
        public void Ties_TheOlderScoreKeepsItsPlace()
        {
            board.Add(100, "Old", 1);
            int place = board.Add(100, "New", 2);
            Assert.AreEqual(1, place);
            Assert.AreEqual("Old", board.entries[0].name);
            Assert.AreEqual("New", board.entries[1].name);
        }

        [Test]
        public void OnlyTheBest10AreKept()
        {
            for (int i = 1; i <= 12; i++) board.Add(i * 10, "P" + i, i);
            Assert.AreEqual(Leaderboard.MaxEntries, board.Count);
            Assert.AreEqual(120, board.entries[0].score);
            Assert.AreEqual(30, board.entries[9].score, "10 and 20 dropped off");
        }

        [Test]
        public void AFullBoard_ATieWithTenthPlace_DoesNotGetIn()
        {
            for (int i = 1; i <= 10; i++) board.Add(i * 10, "P" + i, i);
            Assert.AreEqual(-1, board.Add(10, "Late", 99), "Must beat 10th place, not match it");
            Assert.AreEqual(-1, board.Add(5, "Low", 100));
            Assert.AreEqual(9, board.Add(11, "Just", 101));
            Assert.AreEqual(10, board.Count);
        }

        [Test]
        public void Placeholder_CountsUp_WhenUsed()
        {
            Assert.AreEqual("Player1", board.DefaultName);
            board.Record(10, board.DefaultName, 1);
            Assert.AreEqual("Player2", board.DefaultName);
            board.Record(20, "", 2);
            Assert.AreEqual("Player2", board.entries[0].name, "An empty field uses the placeholder");
            Assert.AreEqual("Player3", board.DefaultName);
        }

        [Test]
        public void ATypedName_IsRemembered_AndStopsThePlaceholderCount()
        {
            board.Record(10, "  Anna  ", 1);
            Assert.AreEqual("Anna", board.entries[0].name, "Trimmed");
            Assert.AreEqual("Anna", board.DefaultName);
            Assert.AreEqual("Player1", board.NextPlaceholder, "The count didn't move");
            board.Record(20, "Bob", 2);
            Assert.AreEqual("Bob", board.DefaultName);
        }

        [Test]
        public void Names_AreCutTo16Characters()
        {
            board.Record(10, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 1);
            Assert.AreEqual("ABCDEFGHIJKLMNOP", board.entries[0].name);
        }

        [Test]
        public void ResultMessages_MatchTheGdd()
        {
            Assert.AreEqual("New best score, Anna!", GC26.Lines.UI.Texts.ResultMessage(0, 300, "Anna"));
            Assert.AreEqual("You're #3, Anna!", GC26.Lines.UI.Texts.ResultMessage(2, 120, "Anna"));
            Assert.AreEqual("120 points didn't make the top 10", GC26.Lines.UI.Texts.ResultMessage(-1, 120, "Anna"));
        }

        [Test]
        public void Tidy_RepairsDataFromAFile()
        {
            board.entries = new System.Collections.Generic.List<ScoreEntry>
            {
                new ScoreEntry(10, "Low", 1), null, new ScoreEntry(-5, "Bad", 2),
                new ScoreEntry(50, "Late", 9), new ScoreEntry(50, "Early", 3), new ScoreEntry(20, "", 4),
            };
            for (int i = 0; i < 10; i++) board.entries.Add(new ScoreEntry(1, "Filler", 100 + i));
            board.lastName = null;
            board.placeholdersUsed = -3;

            board.Tidy();

            Assert.AreEqual(10, board.Count);
            Assert.AreEqual("Early", board.entries[0].name, "Ties: older first");
            Assert.AreEqual("Late", board.entries[1].name);
            Assert.AreEqual("Player", board.entries[2].name, "A blank name gets a name");
            Assert.AreEqual("", board.lastName);
            Assert.AreEqual(0, board.placeholdersUsed);
        }
    }
}
