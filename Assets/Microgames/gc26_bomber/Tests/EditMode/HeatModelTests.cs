using NUnit.Framework;

namespace GC26.Bomber.Tests
{
    public class HeatModelTests
    {
        static HeatModel NewCore() => new HeatModel(HeatSettings.Default);

        [Test]
        public void Shots_AddHeat()
        {
            var core = NewCore();
            core.AddHeat(4f); core.AddHeat(4f);
            Assert.AreEqual(8f, core.Heat, 1e-4);
            Assert.AreEqual(CoreState.Normal, core.State);
        }

        [Test]
        public void Overdrive_StartsAtThreshold_AndBoostsDamage()
        {
            var core = NewCore();
            core.AddHeat(79f);
            Assert.AreEqual(1f, core.DamageMultiplier, 1e-4);
            core.AddHeat(1f);
            Assert.AreEqual(CoreState.Overdrive, core.State);
            Assert.AreEqual(1.3f, core.DamageMultiplier, 1e-4);
        }

        [Test]
        public void Heat_At100_Overheats_AndBlocksFire()
        {
            var core = NewCore();
            int events = 0; core.Overheated += () => events++;
            core.AddHeat(120f);
            Assert.AreEqual(CoreState.Overheated, core.State);
            Assert.IsFalse(core.CanFire);
            Assert.AreEqual(1, events);
            Assert.AreEqual(1, core.OverheatCount);
            core.AddHeat(10f); // ignored while offline
            Assert.AreEqual(100f, core.Heat, 1e-4);
        }

        [Test]
        public void Overheat_Recovers_AfterOverheatSeconds()
        {
            var core = NewCore();
            core.AddHeat(100f);
            core.Tick(1.9f);
            Assert.IsFalse(core.CanFire);
            core.Tick(0.2f);
            Assert.IsTrue(core.CanFire);
            Assert.AreEqual(core.Settings.RecoverTo, core.Heat, 1e-4);
        }

        [Test]
        public void Cooling_WaitsForCoolDelay()
        {
            var core = NewCore();
            core.AddHeat(50f);
            core.Tick(0.4f);
            Assert.AreEqual(50f, core.Heat, 1e-4);
            core.Tick(0.2f);   // passes the 0.5 s delay
            core.Tick(1f);     // 25 heat per second
            Assert.Less(core.Heat, 26f);
        }

        [Test]
        public void Vent_RemovesHeat_ButNotWhileOverheated()
        {
            var core = NewCore();
            core.AddHeat(90f);
            core.Vent(50f);
            Assert.AreEqual(40f, core.Heat, 1e-4);
            core.AddHeat(100f);
            core.Vent(50f);
            Assert.AreEqual(CoreState.Overheated, core.State);
        }
    }

    public class HeatGovernorTests
    {
        [Test]
        public void SafeZone_NeverCrossesOverdrive()
        {
            var core = new HeatModel(HeatSettings.Default);
            var gov = new HeatGovernor();
            const float shotHeat = 4f;
            for (int i = 0; i < 200; i++)
            {
                if (gov.AllowShot(core, shotHeat)) core.AddHeat(shotHeat);
                core.Tick(0.05f);
                Assert.Less(core.Heat, core.Settings.OverdriveAt, $"step {i}");
            }
        }

        [Test]
        public void Forsage_AllowsOverheat_ThenSwitchesOff()
        {
            var core = new HeatModel(HeatSettings.Default);
            var gov = new HeatGovernor { Forsage = true };
            for (int i = 0; i < 40 && core.CanFire; i++) if (gov.AllowShot(core, 4f)) core.AddHeat(4f);
            Assert.AreEqual(CoreState.Overheated, core.State);
            Assert.IsFalse(gov.AllowShot(core, 4f));
            Assert.IsFalse(gov.Forsage);
        }
    }
}
