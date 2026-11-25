using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberQuestTests
    {
        static BomberWorld Hub(BomberMeta m = null) { var w = new BomberWorld { Meta = m ?? new BomberMeta() }; w.EnterHub(); return w; }

        [Test]
        public void KillQuest_TakeProgressTurnIn_UnlocksTheWeapon()
        {
            var w = Hub(new BomberMeta { Rescued = { "mechanic" } });
            Assert.AreEqual("new", w.QuestMark("mechanic"));
            Assert.IsTrue(w.TakeQuest("q_mech1"));
            Assert.IsFalse(w.Meta.WeaponUnlocked("hammer"));
            w.StartRun(3);
            for (int i = 0; i < 25; i++) { w.SpawnForTests("beetle", 272f, 300f, false); var e = w.Enemies[w.Enemies.Count - 1]; w.DamageEnemy(e, 999f); }
            Assert.AreEqual(25, w.Meta.QProg("q_mech1"));
            w.EnterHub();
            Assert.AreEqual("ready", w.QuestMark("mechanic"));
            Assert.IsTrue(w.TurnInQuest("q_mech1"));
            Assert.IsTrue(w.Meta.WeaponUnlocked("hammer"));
            Assert.AreEqual("q_mech2", w.Meta.QuestOf("mechanic").Id, "next quest in the chain");
        }

        [Test]
        public void FetchQuest_ItemsAppearInRooms_AndCount()
        {
            var w = Hub();
            Assert.IsTrue(w.TakeQuest("q_disp1"));
            Assert.IsFalse(w.TakeQuest("q_disp2"), "one quest at a time per NPC");
            w.Meta.QDone.Add("q_disp1"); w.Meta.Quests.Clear();
            Assert.IsTrue(w.TakeQuest("q_disp2"));
            w.StartRun(11);
            int found = 0;
            for (int r = 0; r < 30 && found == 0; r++)
            {
                w.EnterRoomForTests("chest");
                var item = w.Pickups.Find(p => p.Kind == "quest");
                if (item == null) continue;
                w.P.X = item.X; w.P.Y = item.Y;
                w.Tick(1f / 60f, new BomberIntent());
                found = w.Meta.QProg("q_disp2");
            }
            Assert.AreEqual(1, found, "a black box was found and counted");
        }

        [Test]
        public void Overload_UnlocksAfterAWin_AndMakesEnemiesTougher()
        {
            var w = Hub();
            Assert.IsFalse(w.SetOverload(1));
            w.Meta.OlMax = 1; Assert.IsTrue(w.SetOverload(1));
            w.StartRun(5); Assert.AreEqual(1, w.Ol);
            w.SpawnForTests("beetle", 272f, 300f, false);
            Assert.AreEqual(20f * 1.0f * 1.12f, w.Enemies[w.Enemies.Count - 1].MaxHp, 0.01f);
        }

        [Test]
        public void Mk_MakesTheWeaponStronger_ForGood()
        {
            var w = Hub(new BomberMeta { Data = 500 });
            w.StartRun(1); float before = w.Stats(w.P.Weapon).Dmg;
            w.EnterHub();
            Assert.IsTrue(w.BuyMk("blaster")); Assert.IsTrue(w.BuyMk("blaster")); Assert.IsFalse(w.BuyMk("blaster"), "Mk3 is the maximum");
            Assert.AreEqual(500 - 40 - 90, w.Meta.Data);
            w.StartRun(1);
            Assert.AreEqual(before * 1.2f, w.Stats(w.P.Weapon).Dmg, 0.01f);
        }

        [Test]
        public void QuestWeapons_FireWithoutExceptions()
        {
            foreach (var id in new[] { "twin", "hammer", "scatter", "needle", "venom" })
            {
                var w = new BomberWorld(); w.StartRun(4242);
                w.P.Weapons.Clear(); w.P.Weapons.Add(new BomberWeapon { Id = id }); w.P.Cur = 0;
                for (int i = 0; i < 60 * 30 && w.Phase == BomberPhase.Play; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent { AltPressed = i % 90 == 0 }); }
                Assert.Greater(w.Kills, 0, id);
            }
        }
    }
}
