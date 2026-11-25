using NUnit.Framework;
using UnityEngine;

namespace GC26.Bomber.Tests
{
    public class BomberHubTests
    {
        static BomberWorld World(BomberMeta meta = null) => new BomberWorld { Meta = meta ?? new BomberMeta() };

        static BomberMeta Rich()
        {
            var m = new BomberMeta { Data = 5000 };
            foreach (var n in BomberMetaData.Npcs) if (!n.Home) m.Rescued.Add(n.Id);
            foreach (var a in BomberMetaData.Achs) m.Ach.Add(a.Id);
            foreach (var c in BomberMetaData.Chars) if (c.Cost > 0) m.Chars.Add(c.Id);
            return m;
        }

        static Vector2 ToDoor(BomberWorld w) { var d = w.Doors[0]; return new Vector2((d.X0 + d.X1) / 2f - w.P.X, -w.P.Y).normalized; }

        [Test]
        public void Tutorial_CanBeCompleted_AndEndsInTheHub()
        {
            var w = World(); w.EnterTutorial();
            Assert.AreEqual(BomberMode.Tutorial, w.Mode);
            for (int i = 0; i < 60 * 180 && w.Mode == BomberMode.Tutorial; i++)
            {
                var it = new BomberIntent();
                bool press = i % 30 == 0;
                switch (w.TutStep)
                {
                    case 0: it.Move = new Vector2(Mathf.Sin(i * 0.05f), Mathf.Cos(i * 0.05f)); break;
                    case 1: it.Dash = press; break;
                    case 3: it.Vent = press; break;
                    case 4: it.ForsageToggle = press && !w.Governor.Forsage; break;
                    case 5: it.ForsageToggle = press && w.Governor.Forsage; break;
                    case 6: it.Ability = press; break;
                    case 7: it.Move = ToDoor(w); break;
                }
                if (i % 30 == 0) foreach (var e in w.Enemies.ToArray()) w.DamageEnemy(e, 8f);
                w.Tick(1f / 60f, it);
            }
            Assert.AreEqual(BomberMode.Hub, w.Mode, $"stuck on tutorial step {w.TutStep}");
            Assert.IsTrue(w.Meta.TutorialDone);
        }

        [Test]
        public void Tutorial_Skip_GoesToTheHub()
        {
            var w = World(); w.EnterTutorial();
            w.Tick(1f / 60f, new BomberIntent());
            w.SkipTutorial();
            Assert.AreEqual(BomberMode.Hub, w.Mode);
            Assert.IsTrue(w.Meta.TutorialDone);
        }

        [Test]
        public void Hub_TerminalOpensPanel_AndDoorStartsARun()
        {
            var w = World(); w.EnterHub();
            var st = w.Props.Find(p => p.Kind == "station" && p.Id == "chars");
            w.P.X = st.X; w.P.Y = st.Y + 20f;
            w.Tick(1f / 60f, new BomberIntent { Use = true });
            Assert.AreEqual("chars", w.PendingPanel);
            w.P.X = BomberData.W / 2f; w.P.Y = 200f;
            for (int i = 0; i < 60 * 5 && w.Mode == BomberMode.Hub; i++) w.Tick(1f / 60f, new BomberIntent { Move = ToDoor(w) });
            Assert.AreEqual(BomberMode.Run, w.Mode);
            Assert.AreEqual(1, w.Meta.Runs);
        }

        [Test]
        public void Purchases_ChangeTheNextRun()
        {
            var w = World(new BomberMeta { Data = 1000 }); w.EnterHub();
            Assert.IsFalse(w.SelectChar("whisper"), "locked until bought");
            Assert.IsTrue(w.BuyChar("whisper")); Assert.AreEqual("whisper", w.Meta.Char);
            Assert.IsTrue(w.BuyMetaUp("hp"));
            Assert.IsTrue(w.BuyCharUp("whisper", "hp"));
            Assert.IsTrue(w.BuyWeapon("rifle")); Assert.IsTrue(w.SetStartWeapon("rifle"));
            Assert.IsFalse(w.BuyWeapon("rail"), "achievement weapons cannot be bought");
            w.StartRun(9);
            Assert.AreEqual(5 + 1 + 1, w.P.MaxHp);
            Assert.AreEqual("rifle", w.P.Weapon.Id);
            Assert.AreEqual("whisper", w.P.Char);
            Assert.Less(w.Meta.Data, 1000);
        }

        [Test]
        public void EveryDrone_UsesItsAbility_WithoutExceptions()
        {
            foreach (var c in BomberMetaData.Chars)
            {
                var m = Rich(); m.Char = c.Id;
                var w = World(m); w.StartRun(31);
                Assert.AreEqual(c.Id, w.P.Char);
                for (int i = 0; i < 60 * 40 && w.Phase == BomberPhase.Play; i++)
                {
                    w.P.Hp = w.P.MaxHp;
                    var it = new BomberIntent { Ability = i % 120 == 0, Move = new Vector2(Mathf.Sin(i * 0.02f), Mathf.Cos(i * 0.017f)) * 0.6f };
                    if (w.Room.Cleared && w.Doors.Count > 0) it.Move = ToDoor(w);
                    if (w.Reward != null) w.ChooseReward(0);
                    if (w.BenchOpen) w.CloseBench();
                    w.Tick(1f / 60f, it);
                }
                Assert.Greater(w.Kills, 0, c.Id);
            }
        }

        [Test]
        public void Abandon_GivesHalfTheData_AndEndsTheRun()
        {
            var w = World(); w.StartRun(5);
            for (int i = 0; i < 60 * 60 && w.Rooms == 0; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent()); if (i % 30 == 0) foreach (var e in w.Enemies.ToArray()) w.DamageEnemy(e, 10f); }
            Assert.Greater(w.Rooms, 0);
            w.Abandon();
            Assert.AreEqual(BomberPhase.Dead, w.Phase);
            Assert.IsTrue(w.LastAbandoned);
            Assert.AreEqual(w.LastDataGain, w.Meta.Data);
            Assert.Greater(w.LastDataGain, 0);
        }

        [Test]
        public void RescueRoom_FreesASurvivor_WhoBringsABonus()
        {
            BomberWorld w = null;
            for (uint seed = 1; seed < 60; seed++)
            {
                w = World(); w.StartRun(seed);
                if (w.Plan[0].Exists(s => s.Contains("rescue"))) break;
                w = null;
            }
            Assert.NotNull(w, "some seed plans a rescue in sector 1");
            w.EnterRoomForTests("rescue");
            var cell = w.Props.Find(p => p.Kind == "cell"); Assert.NotNull(cell);
            for (int i = 0; i < 60 * 90 && !w.Room.Cleared; i++) { w.P.Hp = w.P.MaxHp; w.Tick(1f / 60f, new BomberIntent()); if (i % 20 == 0) foreach (var e in w.Enemies.ToArray()) w.DamageEnemy(e, 15f); }
            Assert.IsTrue(w.Room.Cleared);
            w.P.X = cell.X; w.P.Y = cell.Y + 20f;
            w.Tick(1f / 60f, new BomberIntent { Use = true });
            Assert.IsTrue(w.Meta.IsRescued(cell.Id));
            Assert.Contains(cell.Id, w.RescuedNow);
        }

        [Test]
        public void Meta_SurvivesJsonRoundTrip()
        {
            var m = new BomberMeta { Data = 77, Char = "sapper", TutorialDone = true };
            m.AddUp("hp"); m.AddUp("hp"); m.AddCharUp("sapper", "dmg"); m.Ach.Add("win"); m.Rescued.Add("doctor");
            var back = JsonUtility.FromJson<BomberMeta>(JsonUtility.ToJson(m));
            Assert.AreEqual(77, back.Data); Assert.AreEqual("sapper", back.Char); Assert.IsTrue(back.TutorialDone);
            Assert.AreEqual(2, back.Up("hp")); Assert.AreEqual(1, back.CharUp("sapper", "dmg"));
            Assert.IsTrue(back.HasAch("win")); Assert.IsTrue(back.IsRescued("doctor"));
            Assert.IsTrue(back.WeaponUnlocked("beam"), "beam is unlocked by the win achievement");
        }
    }
}
