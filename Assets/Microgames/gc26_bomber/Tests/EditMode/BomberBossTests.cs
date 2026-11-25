using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberBossTests
    {
        static BomberWorld NewRun(uint seed = 4242) { var w = new BomberWorld(); w.StartRun(seed); return w; }

        /// <summary>Plays a boss fight with an unkillable drone that dodges around; chips the boss down so the test is quick.</summary>
        static void FightBoss(BomberWorld w, int bi, float seconds)
        {
            w.EnterBossForTests(bi);
            for (int i = 0; i < 60 * seconds && w.Phase == BomberPhase.Play && !w.Room.Cleared; i++)
            {
                w.P.Hp = w.P.MaxHp;
                var it = new BomberIntent { Move = new Vector2(Mathf.Sin(i * 0.02f), Mathf.Cos(i * 0.013f)) * 0.8f, Dash = i % 120 == 0 };
                w.Tick(1f / 60f, it);
                var b = w.CurrentBoss; if (b != null && i % 30 == 0) w.DamageEnemy(b, b.MaxHp * 0.02f);
            }
        }

        [Test]
        public void EveryBoss_SpawnsGoesThroughPhases_AndDies()
        {
            for (int bi = 0; bi < BomberData.Bosses.Length; bi++)
            {
                var w = NewRun();
                int sector = bi < 2 ? 0 : bi < 4 ? 1 : 2;
                if (sector > 0) w.EnterSectorForTests(sector);
                int maxPhase = 0;
                w.EnterBossForTests(bi);
                for (int i = 0; i < 60 * 90 && w.Phase == BomberPhase.Play && !w.Room.Cleared; i++)
                {
                    w.P.Hp = w.P.MaxHp;
                    w.Tick(1f / 60f, new BomberIntent { Move = new Vector2(Mathf.Sin(i * 0.02f), Mathf.Cos(i * 0.013f)) * 0.8f, Dash = i % 120 == 0 });
                    var b = w.CurrentBoss;
                    if (b != null) { Assert.AreEqual(BomberData.Bosses[bi].Key, b.Boss.Key); maxPhase = Mathf.Max(maxPhase, b.Phase); if (i % 30 == 0) w.DamageEnemy(b, b.MaxHp * 0.02f); }
                }
                Assert.IsTrue(w.Room.Cleared, BomberData.Bosses[bi].Name + " was not defeated");
                Assert.AreEqual(BomberData.Bosses[bi].Phases.Length, maxPhase, BomberData.Bosses[bi].Name + " phases");
                Assert.AreEqual(1, w.Bosses);
            }
        }

        [Test]
        public void SectorBoss_OpensNextSector_AndOffersReward()
        {
            var w = NewRun();
            FightBoss(w, 0, 90f);
            Assert.IsTrue(w.Room.Cleared);
            Assert.AreEqual(1, w.Doors.Count); Assert.AreEqual("next", w.Doors[0].Type);
            for (int i = 0; i < 60 && w.Reward == null; i++) w.Tick(1f / 60f, new BomberIntent());
            Assert.NotNull(w.Reward, "reward after the boss");
            w.ChooseReward(0);
            // fly out through the "next sector" door
            for (int i = 0; i < 60 * 10 && w.Sector == 0; i++) { var d = w.Doors[0]; w.Tick(1f / 60f, new BomberIntent { Move = new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized }); }
            Assert.AreEqual(1, w.Sector);
            Assert.AreEqual("start", w.Room.Type);
        }

        [Test]
        public void Oracle_EndsTheRunWithVictory()
        {
            var w = NewRun();
            w.EnterSectorForTests(2);
            FightBoss(w, 4, 120f);
            Assert.IsTrue(w.Room.Cleared);
            for (int i = 0; i < 60 * 3 && w.Phase == BomberPhase.Play; i++) w.Tick(1f / 60f, new BomberIntent());
            Assert.AreEqual(BomberPhase.Victory, w.Phase);
        }

        [Test]
        public void NewEnemies_AndAffixes_RunWithoutExceptions()
        {
            foreach (var kind in new[] { "shield", "sniper", "mortar", "brute", "repair", "phantom", "laser" })
                foreach (var elite in new[] { false, true })
                {
                    var w = NewRun(); w.EnterSectorForTests(2); w.EnterRoomForTests("chest");
                    w.SpawnForTests(kind, 272f, 300f, elite); w.SpawnForTests("beetle", 200f, 260f, false);
                    for (int i = 0; i < 60 * 20 && w.Phase == BomberPhase.Play; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent { Move = new Vector2(Mathf.Sin(i * 0.03f), 0f) }); }
                }
        }

        [Test]
        public void Affixes_ArmorReducesDamage_AndShieldAbsorbs()
        {
            var w = NewRun(); w.EnterRoomForTests("chest");
            for (int k = 0; k < 40; k++) w.SpawnForTests("turret", 100f + k * 5f, 300f, true);
            bool sawArmor = false, sawShield = false;
            foreach (var e in w.Enemies.ToArray())
            {
                float hp = e.Hp;
                if (e.Affix == "armored") { w.DamageEnemy(e, 10f); Assert.AreEqual(hp - 6f, e.Hp, 0.01f); sawArmor = true; }
                if (e.Affix == "shielded") { w.DamageEnemy(e, 10f); Assert.AreEqual(hp, e.Hp, 0.01f); Assert.Greater(e.ShieldHp, 0f); sawShield = true; }
            }
            Assert.IsTrue(sawArmor && sawShield, "40 elites should include both affixes");
        }

        [Test]
        public void LaterSectors_UseTheirEnemyPools()
        {
            var w = NewRun(); w.EnterSectorForTests(2);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int r = 0; r < 6; r++)
            {
                w.EnterRoomForTests("combat");
                for (int i = 0; i < 60 * 6; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent()); foreach (var e in w.Enemies) seen.Add(e.Kind); }
            }
            seen.IntersectWith(new[] { "shield", "sniper", "mortar", "brute", "repair", "phantom", "laser" });
            Assert.Greater(seen.Count, 0, "sector 3 rooms spawn sector 2–3 enemies");
        }
    }
}
