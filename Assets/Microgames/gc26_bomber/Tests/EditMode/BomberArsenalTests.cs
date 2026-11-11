using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberArsenalTests
    {
        static BomberWorld NewRun() { var w = new BomberWorld(); w.StartRun(4242); return w; }

        static void EnterTestRoom(BomberWorld w, string type)
        {
            // jump to a room of the given type through the public test hook
            w.EnterRoomForTests(type);
        }

        [Test]
        public void Chest_OffersReward_AndWeaponTakesSecondSlot()
        {
            var w = NewRun(); EnterTestRoom(w, "chest");
            var chest = w.Props.Find(p => p.Kind == "chest"); Assert.NotNull(chest);
            w.P.X = chest.X; w.P.Y = chest.Y + 20f;
            w.Tick(1f / 60f, new BomberIntent { Use = true });
            Assert.NotNull(w.Reward); Assert.That(w.Reward.Options.Count, Is.InRange(1, 3));
            var weaponIdx = w.Reward.Options.FindIndex(o => o.Type == "weapon");
            if (weaponIdx >= 0) { w.ChooseReward(weaponIdx); Assert.AreEqual(2, w.P.Weapons.Count); Assert.AreEqual(1, w.P.Cur); }
            else { w.ChooseReward(0); }
            Assert.IsNull(w.Reward);
        }

        [Test]
        public void Workbench_UpgradeCostsScrap_AndFusionMakesStormgun()
        {
            var w = NewRun(); EnterTestRoom(w, "workbench");
            w.P.Scrap = 2000;
            w.P.Weapons.Clear(); w.P.Weapons.Add(new BomberWeapon { Id = "minigun" }); w.P.Weapons.Add(new BomberWeapon { Id = "swarm" }); w.P.Cur = 0;
            int before = w.P.Scrap;
            Assert.IsTrue(w.BuyUpgrade(0, "dmg"));
            Assert.Less(w.P.Scrap, before);
            Assert.IsFalse(w.Fuse(), "fusion needs ▲3 on both weapons");
            for (int i = 0; i < 2; i++) w.BuyUpgrade(0, "dmg");
            for (int i = 0; i < 3; i++) w.BuyUpgrade(1, "rate");
            Assert.IsTrue(w.Fuse());
            Assert.AreEqual(1, w.P.Weapons.Count);
            Assert.AreEqual("stormgun", w.P.Weapons[0].Id);
            Assert.AreEqual(3, w.P.Weapons[0].Up["dmg"]);
        }

        [Test]
        public void Shop_BuysItemForScrap()
        {
            var w = NewRun(); EnterTestRoom(w, "shop");
            var item = w.Props.Find(p => p.Kind == "shop" && p.Item.Type != "repair"); Assert.NotNull(item);
            w.P.Scrap = 500; w.P.X = item.X; w.P.Y = item.Y + 20f;
            w.Tick(1f / 60f, new BomberIntent { Use = true });
            Assert.IsTrue(item.Sold);
            Assert.AreEqual(500 - w.PriceOf(item), w.P.Scrap);
        }

        [Test]
        public void EveryWeapon_FiresForAMinute_WithoutExceptions()
        {
            foreach (var id in BomberItems.Weapons.Keys)
            {
                var w = NewRun();
                w.P.Weapons.Clear(); w.P.Weapons.Add(new BomberWeapon { Id = id }); w.P.Cur = 0;
                for (int i = 0; i < 60 * 60 && w.Phase == BomberPhase.Play; i++)
                {
                    var it = new BomberIntent { AltPressed = i % 90 == 0, AltHeld = i % 200 < 30 };
                    if (w.Room.Cleared && w.Doors.Count > 0) { var d = w.Doors[0]; it.Move = new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized; }
                    if (w.Reward != null) w.ChooseReward(0);
                    if (w.BenchOpen) w.CloseBench();
                    w.Tick(1f / 60f, it);
                }
                Assert.Greater(w.Kills, 0, id);
            }
        }

        [Test]
        public void Modules_ArmorAddsHull_AndSetBonusApplies()
        {
            var w = NewRun(); EnterTestRoom(w, "chest");
            int hp = w.P.MaxHp;
            w.GrantForTests(new BomberItem { Type = "module", Id = "armor" });
            Assert.AreEqual(hp + 2, w.P.MaxHp);
            w.GrantForTests(new BomberItem { Type = "module", Id = "shieldm" });
            Assert.IsTrue(w.SetOn("Корпус"));
            Assert.AreEqual(hp + 3, w.P.MaxHp);
        }
    }
}
