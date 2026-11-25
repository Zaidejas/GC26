using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Bomber.Tests
{
    public class BomberToolsTests
    {
        static BomberWorld Run(string tactic = "vent", string ch = "spark")
        {
            var m = new BomberMeta { Tactic = tactic, Char = ch };
            foreach (var t in BomberMetaData.Tactics) m.Tactics.Add(t.Id);
            m.Chars.Add("sapper");
            var w = new BomberWorld { Meta = m }; w.StartRun(77); return w;
        }

        [Test]
        public void EveryTactic_Works_TappedAndAimed()
        {
            foreach (var t in BomberMetaData.Tactics)
            {
                var w = Run(t.Id);
                Assert.AreEqual(t.Id, w.P.Tactic);
                for (int i = 0; i < 60 * 30 && w.Phase == BomberPhase.Play; i++)
                {
                    w.P.Hp = w.P.MaxHp;
                    var it = new BomberIntent { Vent = i % 90 == 0, Move = new Vector2(Mathf.Sin(i * 0.03f), 0.3f) };
                    if (i % 180 == 0) { it.HasAim = true; it.AimAt = new Vector2(w.P.X, w.P.Y - 150f); }
                    w.Tick(1f / 60f, it);
                }
                Assert.Greater(w.Kills, 0, t.Id);
            }
        }

        [Test]
        public void Blink_TeleportsTowardTheAim()
        {
            var w = Run("blink");
            w.Tick(1f / 60f, new BomberIntent());
            float y0 = w.P.Y;
            w.Tick(1f / 60f, new BomberIntent { Vent = true, HasAim = true, AimAt = new Vector2(w.P.X, w.P.Y - 200f) });
            Assert.Less(w.P.Y, y0 - 100f);
            Assert.AreEqual("blink", w.AimKind("tac"));
        }

        [Test]
        public void Sapper_ThrowsDynamiteWhereAimed()
        {
            var w = Run("vent", "sapper");
            Assert.AreEqual("dyn", w.AimKind("ab"));
            w.Tick(1f / 60f, new BomberIntent { Ability = true, HasAim = true, AimAt = new Vector2(100f, 400f) });
            Assert.AreEqual(1, w.Throws.Count);
            Assert.AreEqual(100f, w.Throws[0].Tx, 0.01f); Assert.AreEqual(400f, w.Throws[0].Ty, 0.01f);
            Assert.AreEqual(2, w.P.Charges);
        }

        [Test]
        public void Objectives_PointAtTheDoorAfterClearing()
        {
            var w = Run();
            Assert.AreEqual(0, w.Objectives().Count, "no pointer during the fight");
            w.DevClearRoom();
            Assert.IsTrue(w.Room.Cleared);
            var o = w.Objectives();
            Assert.Greater(o.Count, 0); Assert.Less(o[0].pos.y, BomberData.Cell * 1.5f);
            w.EnterRoomForTests("chest");
            Assert.AreEqual("СУНДУК", w.Objectives()[0].label);
        }

        [Test]
        public void DevTools_JumpToBoss_AndGodMode()
        {
            var w = Run();
            w.DevToBoss(); Assert.AreEqual("boss", w.Room.Type);
            w.DevGod = true; int hp = w.P.Hp;
            for (int i = 0; i < 60 * 20; i++) w.Tick(1f / 60f, new BomberIntent());
            Assert.AreEqual(hp, w.P.Hp);
            w.DevClearRoom();
            Assert.IsTrue(w.Room.Cleared);
        }

        [Test]
        public void DebugLog_RecordsOurErrors_WithGameState()
        {
            var w = Run();
            var log = new BomberDebugLog(() => w.DebugState); log.Attach();
            try
            {
                LogAssert.Expect(LogType.Error, "[gc26_bomber] something broke");
                Debug.LogError("[gc26_bomber] something broke");
                Debug.Log("[gc26_bomber] plain log"); // plain logs are not bugs
                Assert.AreEqual(1, log.Entries.Count);
                StringAssert.Contains("sector=1", log.Entries[0].State);
                Assert.AreEqual(1, log.Unseen);
                StringAssert.Contains("something broke", log.Report());
            }
            finally { log.Detach(); log.Clear(); }
        }
    }
}
