using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberPolishTests
    {
        [Test]
        public void Sector3_HasBlinkingCovers()
        {
            var w = new BomberWorld(); w.StartRun(21); w.EnterSectorForTests(2); w.EnterRoomForTests("combat");
            var fl = w.Walls.FindAll(x => x.Flicker);
            Assert.AreEqual(2, fl.Count);
            bool wasOn = fl[0].On, toggled = false;
            for (int i = 0; i < 60 * 5; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent()); if (fl[0].On != wasOn) toggled = true; }
            Assert.IsTrue(toggled);
        }

        [Test]
        public void SeedModes_DailyAndRepeat()
        {
            var m = new BomberMeta { TutorialDone = true };
            var w = new BomberWorld { Meta = m }; w.StartRun(12345);
            Assert.AreEqual(12345, m.LastSeed);
            w.SetSeedMode("repeat"); w.EnterHub();
            for (int i = 0; i < 60 * 10 && w.Mode == BomberMode.Hub; i++) { var d = w.Doors[0]; w.Tick(1f / 60f, new BomberIntent { Move = new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized }); }
            Assert.AreEqual(12345u, w.Seed);
            w.SetSeedMode("daily"); w.EnterHub();
            for (int i = 0; i < 60 * 10 && w.Mode == BomberMode.Hub; i++) { var d = w.Doors[0]; w.Tick(1f / 60f, new BomberIntent { Move = new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized }); }
            Assert.AreEqual(BomberWorld.DailySeed, w.Seed);
        }

        [Test]
        public void Threat_GrowsWithUpgrades()
        {
            var weak = new BomberWorld(); weak.StartRun(3);
            var m = new BomberMeta(); for (int i = 0; i < 3; i++) { m.AddUp("hp"); m.AddUp("dmg"); m.AddUp("cool"); } m.AddMk("blaster"); m.AddMk("blaster");
            var strong = new BomberWorld { Meta = m }; strong.StartRun(3);
            Assert.AreEqual(1f, weak.ThreatMul, 0.001f);
            Assert.Greater(strong.ThreatMul, 1.1f);
            weak.SpawnForTests("beetle", 272f, 300f, false); strong.SpawnForTests("beetle", 272f, 300f, false);
            Assert.Greater(strong.Enemies[strong.Enemies.Count - 1].MaxHp, weak.Enemies[weak.Enemies.Count - 1].MaxHp);
        }

        [Test]
        public void EveryDrone_HasItsOwnHullSprite()
        {
            var art = new BomberArt();
            try
            {
                var seen = new System.Collections.Generic.HashSet<Sprite>();
                foreach (var c in BomberMetaData.Chars) { var s = art.DroneOf(c); Assert.NotNull(s, c.Id); Assert.IsTrue(seen.Add(s), c.Id); }
            }
            finally { art.Dispose(); }
        }

        [Test]
        public void Audio_SynthesisesEveryEffect()
        {
            var go = new GameObject("audio_test");
            try
            {
                var a = go.AddComponent<BomberAudio>(); a.Init();
                foreach (var n in new[] { "hit", "boom", "hurt", "over", "pick", "tele", "vent", "dash", "door", "ability", "buy", "ach", "combo:7", "shoot:blaster", "shoot:rail", "shoot:flamer", "shoot:beam" })
                    Assert.DoesNotThrow(() => a.Play(n), n);
                Assert.DoesNotThrow(() => a.SetTheme(2));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
