using Lines.Core;
using NUnit.Framework;

namespace Lines.Tests
{
    public class SmokeTests
    {
        [Test]
        public void GameConfig_MatchesGdd()
        {
            Assert.AreEqual(9, GameConfig.BoardSize);
            Assert.AreEqual(7, GameConfig.ColorCount);
            Assert.AreEqual(3, GameConfig.StartBalls);
            Assert.AreEqual(3, GameConfig.BallsPerTurn);
            Assert.AreEqual(5, GameConfig.MinLineLength);
        }
    }
}
