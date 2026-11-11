using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberWorldTests
    {
        static BomberIntent Idle => new BomberIntent();

        [Test]
        public void SameSeed_SameRooms()
        {
            var a = new BomberWorld(); a.StartRun(12345);
            var b = new BomberWorld(); b.StartRun(12345);
            Assert.AreEqual(a.Walls.Count, b.Walls.Count);
            for (int i = 0; i < a.Walls.Count; i++) { Assert.AreEqual(a.Walls[i].X, b.Walls[i].X); Assert.AreEqual(a.Walls[i].Y, b.Walls[i].Y); }
            for (int s = 0; s < 3; s++) Assert.AreEqual(string.Join(",", a.Plan[s].ConvertAll(o => string.Join("/", o))), string.Join(",", b.Plan[s].ConvertAll(o => string.Join("/", o))));
        }

        [Test]
        public void StartRoom_HasExitDoorsInTopWall_AndPlayerAtBottom()
        {
            var w = new BomberWorld(); w.StartRun(7);
            Assert.That(w.Doors.Count, Is.InRange(1, 2));
            foreach (var d in w.Doors) Assert.AreEqual(0f, d.Blocker.Y);
            Assert.Greater(w.P.Y, BomberData.H * 0.7f);
        }

        [Test]
        public void TwoMinutesOfAutoFight_NoExceptions_AndEnemiesDie()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                var w = new BomberWorld(); w.StartRun(seed);
                for (int i = 0; i < 60 * 120 && w.Phase == BomberPhase.Play; i++)
                {
                    var intent = Idle;
                    // tiny bot: walk toward the nearest enemy, leave through a door when the room is clear
                    if (w.Enemies.Count > 0) { var e = w.Enemies[0]; intent.Move = new Vector2(e.X - w.P.X, e.Y - w.P.Y).normalized * 0.4f; }
                    else if (w.Room.Cleared && w.Doors.Count > 0) { var d = w.Doors[0]; intent.Move = new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized; }
                    if (i % 300 == 0) intent.Vent = true;
                    w.Tick(1f / 60f, intent);
                }
                Assert.Greater(w.Kills, 0, $"seed {seed}");
            }
        }

        [Test]
        public void SafeZone_AutoFireNeverOverheats()
        {
            var w = new BomberWorld(); w.StartRun(99);
            for (int i = 0; i < 60 * 40 && w.Phase == BomberPhase.Play; i++)
            {
                w.Tick(1f / 60f, Idle);
                Assert.AreNotEqual(CoreState.Overheated, w.Core.State, $"frame {i}");
            }
        }
    }
}
