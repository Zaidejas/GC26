using NUnit.Framework;

namespace GC26._2048.Tests
{
    public class IdleTimerTests
    {
        [Test]
        public void DefaultIntervalIsFifteenSeconds()
        {
            Assert.AreEqual(15f, new IdleTimer().Interval);
        }

        [Test]
        public void FiresAfterIntervalWithoutInput()
        {
            var timer = new IdleTimer(10f);
            for (int i = 0; i < 99; i++) Assert.IsFalse(timer.Tick(0.1f), $"tick {i}");
            Assert.IsTrue(timer.Tick(0.2f));
        }

        [Test]
        public void FiresAgainEveryIntervalWhileIdle()
        {
            var timer = new IdleTimer(10f);
            Assert.IsTrue(timer.Tick(10f));
            Assert.IsFalse(timer.Tick(9.9f));
            Assert.IsTrue(timer.Tick(0.1f));
        }

        [Test]
        public void InputStartsTheCountOver()
        {
            var timer = new IdleTimer(10f);
            Assert.IsFalse(timer.Tick(9f));
            timer.Reset();
            Assert.IsFalse(timer.Tick(9f));
            Assert.IsTrue(timer.Tick(1f));
        }

        [Test]
        public void LongPauseGivesOnePeekNotABurst()
        {
            var timer = new IdleTimer(10f);
            Assert.IsTrue(timer.Tick(65f));
            Assert.IsFalse(timer.Tick(0.1f));
        }
    }
}
