using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // Tactics (the left "СБРОС" button: Vent, Bubble, Blink, Gravity grenade, Cryo nova, Core dump), drag-to-aim targets,
    // objective pointers for the UI and developer tools (prototype useTactic, smartTarget, aimTarget).
    public sealed partial class BomberWorld
    {
        public BomberTacticDef TacticDef => BomberMetaData.Tactic(P.Tactic);
        public string TacticLabel => TacticDef.Name.ToUpper();
        public float TacticCd => TacticDef.Cd;

        /// <summary>"blink", "grav" or "dyn" when the button is aimed by dragging it; null for a plain tap button.</summary>
        public string AimKind(string button)
        {
            if (Mode == BomberMode.Hub) return null;
            if (button == "ab" && Is("sapper")) return "dyn";
            if (button == "tac" && P.Tactic == "blink") return "blink";
            if (button == "tac" && P.Tactic == "gravity") return "grav";
            return null;
        }
        public static float AimRange(string kind) => kind == "blink" ? 230f : 330f;

        /// <summary>Target for a drag of (dx, dy) screen units out of 120 (full range).</summary>
        public Vector2 AimTarget(string kind, Vector2 drag)
        {
            float l = drag.magnitude, k = Mathf.Min(1f, l / 120f), r = AimRange(kind) * k, a = Mathf.Atan2(drag.y, drag.x);
            return new Vector2(Mathf.Clamp(P.X + Mathf.Cos(a) * r, 40f, W - 40f), Mathf.Clamp(P.Y + Mathf.Sin(a) * r, 40f, H - 40f));
        }

        /// <summary>Tap without dragging: the densest group of enemies for throws; for blink — along the stick or away from the nearest enemy.</summary>
        Vector2 SmartTarget(string kind, Vector2 move)
        {
            if (kind == "blink")
            {
                float a; if (move.sqrMagnitude > 0.04f) a = Mathf.Atan2(move.y, move.x); else { var e = NearestEnemy(P.X, P.Y, 400f); a = e != null ? Ang(e, P) : -Mathf.PI / 2f; }
                return new Vector2(Mathf.Clamp(P.X + Mathf.Cos(a) * 230f, 40f, W - 40f), Mathf.Clamp(P.Y + Mathf.Sin(a) * 230f, 40f, H - 40f));
            }
            BomberEnemy best = null; float bs = -1f;
            foreach (var e in Enemies)
            {
                if (e.Alpha < 0.5f || Dist(e, P) > AimRange(kind) + 60f) continue;
                float sc = 0f; foreach (var o in Enemies) if (Dist(o, e) < 95f) sc += o.Elite || o.IsBoss ? 2f : 1f;
                sc -= Dist(e, P) / 1000f; if (sc > bs) { bs = sc; best = e; }
            }
            return best != null ? new Vector2(best.X, best.Y) : new Vector2(P.X + Mathf.Cos(P.Aim) * 200f, P.Y + Mathf.Sin(P.Aim) * 200f);
        }

        Vector2 TargetFor(string kind, BomberIntent it) => it.HasAim ? new Vector2(Mathf.Clamp(it.AimAt.x, 40f, W - 40f), Mathf.Clamp(it.AimAt.y, 40f, H - 40f)) : SmartTarget(kind, it.Move);

        void UseTactic(BomberIntent it)
        {
            if (P.TacCd > 0f || !Core.CanFire) return;
            var t = TacticDef; P.TacCd = t.Cd;
            switch (t.Id)
            {
                case "vent": Vent(); break;
                case "bubble": P.BubbleT = 3f; Core.Vent(20f); AddRing(P.X, P.Y, 46f, new Color32(159, 208, 255, 255), 0.3f); break;
                case "blink":
                {
                    var tg = TargetFor("blink", it); float a = Mathf.Atan2(tg.y - P.Y, tg.x - P.X), d = Mathf.Min(230f, Dist(tg.x, tg.y, P.X, P.Y));
                    while (d > 0f && SolidAt(P.X + Mathf.Cos(a) * d, P.Y + Mathf.Sin(a) * d, P.R)) d -= 8f;
                    for (int i = 0; i < 12; i++) Particle(P.X, P.Y, (Random.value - 0.5f) * 200f, (Random.value - 0.5f) * 200f, 0.3f, CharDef.Color, 3f);
                    P.X += Mathf.Cos(a) * d; P.Y += Mathf.Sin(a) * d; PushOut(P); P.ITime = Mathf.Max(P.ITime, 0.3f); Core.Vent(20f);
                    AddRing(P.X, P.Y, 34f, CharDef.Color, 0.3f);
                    break;
                }
                case "gravity":
                {
                    var tg = TargetFor("grav", it);
                    Throws.Add(new BomberThrow { Kind = "grav", Sx = P.X, Sy = P.Y, X = P.X, Y = P.Y, Tx = tg.x, Ty = tg.y, Fly = 0.35f, Fuse = 1.6f });
                    break;
                }
                case "cryonova":
                    Core.Vent(40f); AddRing(P.X, P.Y, 150f, new Color32(159, 230, 255, 255), 0.5f);
                    foreach (var e in Enemies) if (Dist(e, P) < 150f) e.Stun = Mathf.Max(e.Stun, e.IsBoss ? 0.8f : 2f);
                    break;
                case "overload":
                {
                    float h = Core.Heat; int dmg = Mathf.RoundToInt(h * (h >= 90f ? 1.8f : 1.2f)); Core.Vent(HeatModel.Max);
                    Effects.Add(new BomberEffect { Kind = "blast", X = P.X, Y = P.Y, R = 130f, T = 0.4f, Max = 0.4f }); AddRing(P.X, P.Y, 130f, new Color32(255, 179, 71, 255), 0.4f); Shake?.Invoke(0.8f);
                    foreach (var e in Enemies.ToArray()) if (Dist(e, P) < 130f + e.R) DamageEnemy(e, dmg, Ang(P, e), 300f);
                    foreach (var b in Shots) if (b.Enemy && Dist(b.X, b.Y, P.X, P.Y) < 130f) b.Dead = true;
                    Texts.Add(new BomberText { X = P.X, Y = P.Y - 26f, S = $"ВЫБРОС {dmg}", T = 1f, Color = new Color32(255, 179, 71, 255) });
                    break;
                }
            }
        }

        // ---- hub: tactics terminal ----
        public bool SelectTactic(string id) { if (!Meta.TacticUnlocked(id)) return false; Meta.Tactic = id; SaveMeta(); RefreshHubPlayer(); return true; }
        public bool BuyTactic(string id) { var t = BomberMetaData.Tactic(id); if (t.Id != id || Meta.TacticUnlocked(id) || Meta.Data < t.Cost) return false; Meta.Tactics.Add(id); Spend(t.Cost); return SelectTactic(id); }

        // ================= objective pointers =================
        /// <summary>Where the player should go next (door, unopened chest, freed survivor) with a short label; empty during a fight.</summary>
        public List<(Vector2 pos, string label, Color32 color)> Objectives()
        {
            var o = new List<(Vector2, string, Color32)>();
            if (Phase != BomberPhase.Play || Room == null) return o;
            if (Mode == BomberMode.Tutorial && TutStep < 7) return o;
            if (Mode == BomberMode.Hub)
                foreach (var n in Npcs) if (QuestMark(n.Id) == "ready") o.Add((new Vector2(n.X, n.Y), "СДАТЬ КВЕСТ", BomberData.C("#6dffb0")));
            if (Mode == BomberMode.Run && Room.Locked && !Room.Cleared) return o;
            foreach (var p in Props)
            {
                if (p.Opened) continue;
                if (p.Kind == "chest") { o.Add((new Vector2(p.X, p.Y), "СУНДУК", BomberData.C("#7fe3ff"))); return o; }
                if (p.Kind == "cell" && Room.Cleared) { var n = BomberMetaData.Npc(p.Id); o.Add((new Vector2(p.X, p.Y), "ВЫЖИВШИЙ", n.Color)); return o; }
            }
            foreach (var d in Doors) if (d.Open) o.Add((new Vector2((d.X0 + d.X1) / 2f, Cell * 0.6f), BomberData.RoomLabel[d.Type], BomberData.RoomColor[d.Type]));
            return o;
        }

        /// <summary>Last few enemies of a wave: the UI points at the ones that are off screen.</summary>
        public bool HuntMode => Mode != BomberMode.Hub && Room != null && Room.Locked && !Room.Cleared && Enemies.Count > 0 && Enemies.Count <= 3 && !Effects.Exists(e => e.Kind == "spawn");

        // ================= developer tools (editor / development builds) =================
        public bool DevGod;

        public void DevToBoss()
        {
            if (Mode != BomberMode.Run || Phase != BomberPhase.Play) return;
            Step = Plan[Sector].Count - 1; RoomDamaged = false; EnterRoom("boss");
        }

        public void DevClearRoom()
        {
            if (Mode == BomberMode.Hub || Room == null || Room.Cleared) return;
            Effects.RemoveAll(e => e.Kind == "spawn");
            var boss = CurrentBoss; if (boss != null) { DamageEnemy(boss, boss.Hp + 1f); return; }
            foreach (var e in Enemies.ToArray()) DamageEnemy(e, e.Hp + e.ShieldHp + 1f);
            Enemies.Clear();
            if (Room.Locked) { Room.Wave = Room.Waves.Count - 1; Room.StartDelay = 0f; RoomCleared(); }
        }

        public void DevUnlockAll()
        {
            foreach (var a in BomberMetaData.Achs) if (!Meta.HasAch(a.Id)) Meta.Ach.Add(a.Id);
            foreach (var n in BomberMetaData.Npcs) if (!n.Home && !Meta.IsRescued(n.Id)) Meta.Rescued.Add(n.Id);
            foreach (var c in BomberMetaData.Chars) if (c.Cost > 0 && !Meta.Chars.Contains(c.Id)) Meta.Chars.Add(c.Id);
            foreach (var kv in BomberMetaData.WeaponUnlock) if (!Meta.Weapons.Contains(kv.Key)) Meta.Weapons.Add(kv.Key);
            foreach (var t in BomberMetaData.Tactics) if (!Meta.Tactics.Contains(t.Id)) Meta.Tactics.Add(t.Id);
            SaveMeta(); RefreshHubPlayer();
        }

        public void DevAddData(int n) { Meta.Data += n; SaveMeta(); }
        public void DevAddScrap(int n) { P.Scrap += n; }

        public void DevResetProgress() { Meta = new BomberMeta(); SaveMeta(); EnterTutorial(); }

        /// <summary>One line about the game state, written next to every logged error.</summary>
        public string DebugState =>
            $"mode={Mode} phase={Phase} sector={Sector + 1} step={Step} room={Room?.Type} seed={Seed} char={P.Char} weapon={(P.Weapons.Count > 0 ? P.Weapon.Id : "-")} " +
            $"hp={P.Hp}/{P.MaxHp} heat={Core.Heat:0} enemies={Enemies.Count} shots={Shots.Count} boss={CurrentBoss?.Boss.Key ?? "-"} t={Time:0.0}";
    }
}
