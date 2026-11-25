using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public sealed class BomberThrow { public string Kind = "dyn"; public float Sx, Sy, Tx, Ty, X, Y, T, Fly = 0.4f, Fuse = 0.7f; }
    public sealed class BomberTurret { public float X, Y, T = 9f, Cd, A; }

    // Playable drones: stats, traits and abilities (prototype CHARS, useAbility, updateAbility).
    public sealed partial class BomberWorld
    {
        public readonly List<BomberThrow> Throws = new List<BomberThrow>();
        public readonly List<BomberTurret> Turrets = new List<BomberTurret>();

        public BomberCharDef CharDef => BomberMetaData.Char(P.Char);
        bool Is(string ch) => P.Char == ch;
        public bool Cloaked => Is("whisper") && P.AbT > 0f;
        bool Adrenaline => Is("dart") && P.AbT > 0f;
        public bool TimeSlowed => Is("chrono") && P.AbT > 0f;
        /// <summary>Enemies, their bullets and hazards run at this speed (Хронос slows them down).</summary>
        float EnemyTimeK => TimeSlowed ? 1f / 3f : 1f;

        /// <summary>Applies the chosen drone and the bought upgrades to a fresh player.</summary>
        void SetupPlayer()
        {
            var m = Meta;
            var ch = BomberMetaData.Char(m.CharUnlocked(m.Char) ? m.Char : "spark");
            P.Char = ch.Id;
            int cuHp = m.CharUp(ch.Id, "hp"), cuDmg = m.CharUp(ch.Id, "dmg"), cuSpd = m.CharUp(ch.Id, "spd"), cuAb = m.CharUp(ch.Id, "abil");
            P.BaseHp = P.MaxHp = P.Hp = ch.Hp + m.Up("hp") + (m.IsRescued("doctor") ? 1 : 0) + cuHp;
            P.Speed = ch.Speed * (1f + 0.05f * cuSpd);
            P.DmgK = ch.Dmg * (1f + 0.06f * cuDmg) * (1f + 0.06f * m.Up("dmg"));
            P.CoolK = ch.Cool * (1f + 0.1f * m.Up("cool"));
            P.DashK = ch.Dash * (1f - 0.08f * cuSpd);
            P.AbilityCd = ch.Cd * (1f - 0.1f * cuAb);
            P.Emp = 1 + m.Up("emp");
            P.Scrap = 25 * m.Up("scrap") + (m.IsRescued("quarter") ? 15 : 0);
            P.ModSlots = 4 + m.Up("slot"); P.CompSlots = 1 + m.Up("comp");
            P.Charges = 3; P.ChargeT = 0f; P.LeechK = 0; P.VortT = 0f; P.BubbleT = 0f;
            P.Tactic = m.TacticUnlocked(m.Tactic) ? m.Tactic : "vent";
            P.Weapons.Clear(); P.Weapons.Add(new BomberWeapon { Id = m.WeaponUnlocked(m.StartWeapon) ? m.StartWeapon : "blaster" }); P.Cur = 0;
            Throws.Clear(); Turrets.Clear();
        }

        public string AbilityLabel => CharDef.Ability.ToUpper();
        /// <summary>0..1 readiness of the ability button and the text under it.</summary>
        public (float ready, string sub) AbilityState
        {
            get
            {
                if (Is("sapper")) return (P.Charges > 0 ? 1f : Mathf.Clamp01(1f - P.ChargeT / P.AbilityCd), "×" + P.Charges);
                if (P.AbT > 0f) return (1f, P.AbT.ToString("0.0"));
                float r = Mathf.Clamp01(1f - P.AbCd / P.AbilityCd);
                return (r, r < 1f ? Mathf.CeilToInt(P.AbCd).ToString() : "");
            }
        }

        void UseAbility(BomberIntent it)
        {
            var ch = CharDef;
            if (Is("sapper"))
            {
                if (P.Charges <= 0) return;
                P.Charges--; if (P.ChargeT <= 0f) P.ChargeT = P.AbilityCd;
                var tg = TargetFor("dyn", it);
                Throws.Add(new BomberThrow { Sx = P.X, Sy = P.Y, X = P.X, Y = P.Y, Tx = tg.x, Ty = tg.y });
                return;
            }
            if (P.AbCd > 0f || P.AbT > 0f) return;
            P.AbT = Is("whisper") || Is("chrono") ? 4f : Is("leech") ? 3f : Is("forge") ? 0.01f : 5f; P.AbCd = P.AbilityCd;
            AddRing(P.X, P.Y, 40f, ch.Color, 0.4f); Sound("ability");
            switch (P.Char)
            {
                case "whisper": foreach (var e in Enemies) if (e.Kind == "sniper" && e.St == "aim") { e.St = "idle"; e.Tm = 1.5f; } Toast?.Invoke("Невидимость", 1f); break;
                case "forge": Turrets.Add(new BomberTurret { X = P.X + Mathf.Cos(P.Aim) * 24f, Y = P.Y + Mathf.Sin(P.Aim) * 24f, A = P.Aim }); Toast?.Invoke("Турель", 1f); break;
                case "chrono": Toast?.Invoke("Время замедлено", 1f); break;
                case "leech": Toast?.Invoke("Вихрь", 1f); break;
                case "bastion": Toast?.Invoke("Броня", 1f); break;
                case "dart": Toast?.Invoke("Адреналин", 1f); break;
                default: Toast?.Invoke("Разгон: выстрелы не греют ядро", 1f); break;
            }
        }

        void UpdateAbility(float dt)
        {
            if (Is("sapper")) { if (P.Charges < 3) { P.ChargeT -= dt; if (P.ChargeT <= 0f) { P.Charges++; P.ChargeT = P.Charges < 3 ? P.AbilityCd : 0f; } } }
            else if (P.AbT > 0f) { P.AbT -= dt; if (P.AbT <= 0f && Is("whisper")) Toast?.Invoke("Тебя снова видно", 1f); }
            else P.AbCd -= dt;

            if (Is("bastion") && P.AbT > 0f)
                foreach (var e in Enemies.ToArray()) { e.SlamT -= dt; if (e.SlamT <= 0f && Dist(e, P) < e.R + P.R + 4f) { e.SlamT = 0.5f; DamageEnemy(e, 25f, Ang(P, e), 420f); Shake?.Invoke(0.3f); } }
            if (Is("leech") && P.AbT > 0f)
            {
                P.VortT -= dt;
                foreach (var e in Enemies) { float d0 = Dist(e, P); if (d0 < 170f && d0 > P.R + e.R && !Anchored(e)) { float a = Ang(e, P); MoveBody(e, Mathf.Cos(a) * 200f * dt, Mathf.Sin(a) * 200f * dt); } }
                if (P.VortT <= 0f) { P.VortT = 0.2f; foreach (var e in Enemies.ToArray()) if (Dist(e, P) < 170f) DamageEnemy(e, 6f, 0f, 0f, true); }
                if (Random.value < 0.8f) { float a = Random.value * 6.28f, r = 60f + Random.value * 110f; Particle(P.X + Mathf.Cos(a) * r, P.Y + Mathf.Sin(a) * r, -Mathf.Cos(a) * 200f, -Mathf.Sin(a) * 200f, 0.4f, new Color32(255, 77, 141, 255), 2f); }
            }
        }

        void UpdateGadgets(float dt)
        {
            for (int i = Throws.Count - 1; i >= 0; i--)
            {
                var t = Throws[i];
                if (t.T < t.Fly) { t.T += dt; float k = Mathf.Min(1f, t.T / t.Fly); t.X = t.Sx + (t.Tx - t.Sx) * k; t.Y = t.Sy + (t.Ty - t.Sy) * k - Mathf.Sin(k * Mathf.PI) * 50f; continue; }
                t.X = t.Tx; t.Y = t.Ty; t.Fuse -= dt;
                if (t.Kind == "grav")
                {
                    foreach (var e in Enemies) { float d0 = Dist(e.X, e.Y, t.X, t.Y); if (d0 < 150f && d0 > 6f && !Anchored(e)) { float a = Mathf.Atan2(t.Y - e.Y, t.X - e.X); MoveBody(e, Mathf.Cos(a) * 260f * dt, Mathf.Sin(a) * 260f * dt); } }
                    if (Random.value < 0.8f) { float a = Random.value * 6.28f; Particle(t.X + Mathf.Cos(a) * 140f, t.Y + Mathf.Sin(a) * 140f, -Mathf.Cos(a) * 300f, -Mathf.Sin(a) * 300f, 0.45f, new Color32(180, 140, 255, 255), 2f); }
                    if (t.Fuse <= 0f) { Throws.RemoveAt(i); Explode(t.X, t.Y, 90f, 45f * (1f + 0.06f * Meta.Up("dmg")), false, true); }
                    continue;
                }
                if (Random.value < 0.5f) Particle(t.X, t.Y - 6f, (Random.value - 0.5f) * 30f, -60f, 0.3f, new Color32(255, 214, 92, 255), 2f);
                if (t.Fuse <= 0f) { Throws.RemoveAt(i); Explode(t.X, t.Y, 95f, 80f * (1f + 0.06f * Meta.Up("dmg")), false, true); }
            }
            for (int i = Turrets.Count - 1; i >= 0; i--)
            {
                var t = Turrets[i]; t.T -= dt; t.Cd -= dt;
                if (t.T <= 0f) { Turrets.RemoveAt(i); for (int k = 0; k < 10; k++) Spark(t.X, t.Y, new Color32(255, 159, 67, 255), 150f); continue; }
                BomberEnemy best = null; float bd = 380f;
                foreach (var e in Enemies) { if (e.Alpha < 0.5f) continue; float d0 = Dist(e.X, e.Y, t.X, t.Y); if (d0 < bd && Los(t.X, t.Y, e.X, e.Y)) { bd = d0; best = e; } }
                if (best == null) continue;
                t.A = Mathf.Atan2(best.Y - t.Y, best.X - t.X);
                if (t.Cd <= 0f) { t.Cd = 0.22f; Shot(t.X + Mathf.Cos(t.A) * 12f, t.Y + Mathf.Sin(t.A) * 12f, t.A + (_rngC.Next() - 0.5f) * 0.08f, 700f, 8f * (1f + 0.06f * Meta.Up("dmg")), new BomberShot { R = 3f, Life = 0.9f, Color = new Color32(255, 207, 138, 255) }); }
            }
        }

        /// <summary>Wandering direction for enemies that lost the cloaked drone.</summary>
        Vector2 Wander(BomberEnemy e) { float a = Time * 0.7f + e.X * 0.01f + e.Orbit; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f; }
    }
}
