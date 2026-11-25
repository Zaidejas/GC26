using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // Weapons, upgrades, modules, companions, rewards, shop and workbench — ported from the prototype
    // (ws, fire, fireBeam, fireArc, updateBullets, updateComps, lootOptions, grant, makeShop, interact, doFusion).
    public sealed partial class BomberWorld
    {
        public readonly List<BomberProp> Props = new List<BomberProp>();
        public readonly List<BomberBolt> Bolts = new List<BomberBolt>();
        public readonly List<BomberTrail> Trails = new List<BomberTrail>();
        public BomberReward Reward { get; private set; }
        /// <summary>Set when the player opens the workbench; the UI shows the upgrade panel until CloseBench().</summary>
        public bool BenchOpen { get; private set; }

        // ================= modules & sets =================
        public bool HasMod(string id) => P.Mods.Contains(id);
        public bool HasComp(string id) => P.Comps.Exists(c => c.Id == id);
        public bool SetOn(string cat) { int n = 0; foreach (var m in P.Mods) if (BomberItems.Mods[m].Cat == cat) n++; return n >= 2; }
        float CoolMul => P.CoolK * (HasMod("radiator") ? 1.4f : 1f) * (SetOn("Ядро") ? 1.15f : 1f) * (Combo >= 10 ? 1.2f : 1f);
        float DmgMul => P.DmgK * (Cloaked ? 3f : 1f) * Core.DamageMultiplier * (HasMod("heavy") ? 1.25f : 1f) * (SetOn("Снаряды") ? 1.1f : 1f);
        float Discount => HasMod("discount") ? 0.75f : 1f;
        public int PriceOf(BomberProp p) => Mathf.RoundToInt(p.Price * Discount);

        void ApplyCoreMods()
        {
            var s = Core.Settings; s.OverdriveAt = HasMod("mad") ? 60f : 80f; s.OverheatSeconds = HasMod("mad") ? 3f : 2f; Core.Settings = s;
        }

        void RecalcHp()
        {
            int m = P.BaseHp + (HasMod("armor") ? 2 : 0) + (SetOn("Корпус") ? 1 : 0) - P.AltarLoss;
            if (m > P.MaxHp) P.Hp += m - P.MaxHp;
            P.MaxHp = Mathf.Max(1, m); P.Hp = Mathf.Min(P.Hp, P.MaxHp);
        }

        // ================= weapon stats with upgrades (prototype ws) =================
        public BomberWeaponDef Stats(BomberWeapon w)
        {
            var d = w.Def; var s = d.Clone(); var u = w.Up; int mk = d.Fused ? 2 : Meta.Mk(w.Id);
            float dm = 1f + 0.2f * u["dmg"] + 0.1f * mk;
            s.Dmg = d.Dmg * dm; s.Rate = d.Rate * (1f + 0.15f * u["rate"] + 0.08f * mk); s.Heat = d.Heat * (1f - 0.15f * u["heat"]);
            int k = u["spec"];
            switch (d.Spec)
            {
                case "pierce": s.Pierce += k; break;
                case "pellets": s.Pellets += 2 * k; break;
                case "rockets": s.Pellets += k; break;
                case "accuracy": s.Spread *= 1f - 0.3f * k; s.Grow *= 1f - 0.3f * k; break;
                case "bounce": s.Bounce += k; break;
                case "aoe": s.Aoe *= 1f + 0.2f * k; break;
                case "dmg2": s.Dmg *= 1f + 0.25f * k; break;
                case "range": s.Life *= 1f + 0.3f * k; break;
                case "chains": s.Chains += k; break;
                case "slow": s.Slow += k; break;
            }
            if (d.AltW != null) { s.AltW = d.AltW.Clone(); s.AltW.Dmg = d.AltW.Dmg * dm; s.AltW.Heat = d.AltW.Heat * (1f - 0.15f * u["heat"]); }
            return s;
        }

        /// <summary>Label of the alt-fire button for the current weapon, or null if it has none.</summary>
        public string AltLabel { get { var d = P.Weapon.Def; return d.AltW != null ? "АЛЬТ" : d.Alt == "charge" ? "ЗАРЯД" : d.Alt == "double" ? "ДУПЛЕТ" : d.Alt == "grenade" ? "ГРАНАТА" : null; } }

        // ================= firing =================
        void UpdateWeapons(float dt, BomberIntent it)
        {
            var w = P.Weapon; var s = Stats(w);
            foreach (var ww in P.Weapons) { ww.Cd -= dt; ww.AltCd -= dt; }
            bool firing = P.Firing;
            if (firing && s.Grow > 0f) w.Grow = Mathf.Min(0.35f, w.Grow + dt * s.Grow); else w.Grow = Mathf.Max(0f, w.Grow - dt * 0.6f);
            if (s.Kind == "mini") { float sp = 1f + w.Up["spec"]; if (firing && Core.CanFire) w.Spin = Mathf.Min(1f, w.Spin + dt * sp); else w.Spin = Mathf.Max(0f, w.Spin - dt * 1.5f); }
            P.Beam.On = false;
            float rateMul = (Adrenaline ? 1.3f : 1f) * (Governor.Forsage ? 1.15f : 1f) * (HasMod("heavy") ? 0.85f : 1f) * (s.Kind == "mini" ? 0.2f + 0.8f * w.Spin : 1f);
            if (firing && Core.CanFire)
            {
                if (s.Kind == "beam") FireBeam(s, w);
                else if (w.Cd <= 0f) { if (s.Kind == "arc") FireArc(s); else Fire(s, w, 1f, 1f, s.Pellets); AddCoreHeat(s.Heat); w.Cd = 1f / (s.Rate * rateMul); }
            }
            if (s.AltW != null)
            {
                bool want = s.AltW.AltCd < 0.4f ? it.AltHeld || it.AltPressed : it.AltPressed;
                if (want && Core.CanFire && w.AltCd <= 0f)
                {
                    var a2 = s.Clone(); var aw = s.AltW;
                    a2.Kind = aw.Kind; a2.Dmg = aw.Dmg; a2.Pellets = aw.Pellets; a2.Spread = aw.Spread; a2.Speed = aw.Speed > 0 ? aw.Speed : s.Speed; a2.R = aw.R; a2.Aoe = aw.Aoe; a2.Homing = aw.Homing;
                    a2.Life = aw.Life; a2.Pierce = aw.Pierce; a2.Bounce = aw.Bounce; a2.Burn = aw.Burn; a2.Slow = aw.Slow; a2.Chains = aw.Chains; a2.Range = aw.Range; a2.Look = aw.Look; a2.Trail = false; a2.Zap = aw.Zap; a2.Grow = 0;
                    if (a2.Kind == "arc") FireArc(a2); else Fire(a2, w, 1f, 1f, Mathf.Max(1, a2.Pellets));
                    AddCoreHeat(aw.Heat); w.AltCd = aw.AltCd;
                }
            }
            else if (it.AltPressed && Core.CanFire && s.Alt != null && w.AltCd <= 0f)
            {
                if (s.Alt == "charge") { Fire(s, w, 3f, 1.1f, 1, 7f, null, true); AddCoreHeat(s.Heat * 3f); w.AltCd = 0.8f; Shake?.Invoke(0.5f); }
                if (s.Alt == "double") { Fire(s, w, 1f, 1f, s.Pellets * 2, 0f, 0.6f); AddCoreHeat(s.Heat * 2f); w.AltCd = 1.2f; w.Cd = 1f / s.Rate; }
                if (s.Alt == "grenade") { Shot(P.X, P.Y, P.Aim, 420f, 30f * DmgMul, new BomberShot { R = 6, Aoe = 70, Life = 0.7f, Color = new Color32(159, 255, 207, 255) }); AddCoreHeat(15f); w.AltCd = 1.5f; }
            }
            P.AltCd = w.AltCd;
        }

        void AddCoreHeat(float h) { if (!(Is("spark") && P.AbT > 0f)) Core.AddHeat(h * (Is("forge") ? 0.6f : 1f)); }

        void Fire(BomberWeaponDef s, BomberWeapon w, float dm, float spd, int pellets, float rOverride = 0f, float? spreadOverride = null, bool heavy = false)
        {
            float spread = (spreadOverride ?? s.Spread) + w.Grow;
            for (int i = 0; i < pellets; i++)
            {
                float a = P.Aim;
                if (pellets > 1) a += (i / (float)(pellets - 1) - 0.5f) * spread * 2f + (_rngC.Next() - 0.5f) * 0.06f; else a += (_rngC.Next() - 0.5f) * 2f * spread;
                float speed = s.Speed * spd * (SetOn("Снаряды") ? 1.1f : 1f) * (pellets > 1 ? 0.92f + _rngC.Next() * 0.16f : 1f);
                Shot(P.X + Mathf.Cos(P.Aim) * 14f, P.Y + Mathf.Sin(P.Aim) * 14f, a, speed, s.Dmg * dm * DmgMul, new BomberShot
                {
                    R = rOverride > 0f ? rOverride : s.R, Pierce = s.Pierce, Bounce = s.Bounce, Aoe = s.Aoe, Kb = s.Kb, Life = s.Life, Trail = s.Trail, Homing = s.Homing, Burn = s.Burn, Slow = s.Slow, Zap = s.Zap, Heavy = heavy,
                    Look = s.Look, Color = s.Col.a > 0 ? s.Col : s.Burn ? new Color32(255, 154, 60, 255) : s.Slow > 0 ? new Color32(159, 230, 255, 255) : s.Zap ? new Color32(217, 240, 255, 255) : Core.State == CoreState.Overdrive ? new Color32(255, 227, 154, 255) : new Color32(216, 248, 255, 255),
                });
            }
            P.MuzzleT = 0.06f; Sound("shoot:" + w.Id);
            if (s.Trail) Shake?.Invoke(0.4f); else if (pellets > 3) Shake?.Invoke(0.25f);
        }

        void Shot(float x, float y, float a, float speed, float dmg, BomberShot o)
        {
            o.X = x; o.Y = y; o.Vx = Mathf.Cos(a) * speed; o.Vy = Mathf.Sin(a) * speed; o.Dmg = dmg; o.Split = HasMod("splitter");
            if (o.Color.a == 0) o.Color = new Color32(216, 248, 255, 255);
            Shots.Add(o);
        }

        /// <summary>Where a ray from (x, y) at angle a hits a wall (used by the view to draw beams).</summary>
        public Vector2 RayEndPublic(float x, float y, float a, float max) => RayEnd(x, y, a, max);

        Vector2 RayEnd(float x, float y, float a, float max)
        {
            float dx = Mathf.Cos(a), dy = Mathf.Sin(a), d = 0f;
            while (d < max) { d += 6f; if (SolidAt(x + dx * d, y + dy * d, 1f)) break; }
            return new Vector2(x + dx * d, y + dy * d);
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l = dx * dx + dy * dy; if (l < 1e-4f) l = 1f;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / l);
            return Dist(px, py, ax + dx * t, ay + dy * t);
        }

        void FireBeam(BomberWeaponDef s, BomberWeapon w)
        {
            Sound("shoot:beam");
            float sx = P.X + Mathf.Cos(P.Aim) * 14f, sy = P.Y + Mathf.Sin(P.Aim) * 14f;
            var end = RayEnd(sx, sy, P.Aim, 700f); float len = Dist(sx, sy, end.x, end.y), hd = len; BomberEnemy hitE = null;
            foreach (var e in Enemies) { if (e.Alpha < 0.5f) continue; if (SegDist(e.X, e.Y, sx, sy, end.x, end.y) < e.R + 3f) { float along = Dist(e.X, e.Y, sx, sy); if (along < hd) { hd = along; hitE = e; } } }
            P.Beam = new BomberBeam { On = true, Zap = s.Zap, Sx = sx, Sy = sy, Ex = sx + Mathf.Cos(P.Aim) * hd, Ey = sy + Mathf.Sin(P.Aim) * hd };
            if (w.Cd <= 0f)
            {
                w.Cd = 1f / s.Rate; AddCoreHeat(s.Heat);
                if (hitE != null) { DamageEnemy(hitE, s.Dmg * DmgMul, P.Aim, 20f, false, s.Burn, s.Slow); Spark(P.Beam.Ex, P.Beam.Ey, s.Zap ? new Color32(217, 240, 255, 255) : new Color32(255, 154, 223, 255), 100f); if (s.Zap && _rngC.Next() < 0.25f && Enemies.Contains(hitE)) ChainZap(hitE, 10f * DmgMul, 2); }
            }
        }

        void ChainZap(BomberEnemy from, float dmg, int n)
        {
            var hit = new HashSet<BomberEnemy> { from }; var cur = from; var bolt = new BomberBolt { T = 0.12f }; bolt.Pts.Add(new Vector2(from.X, from.Y));
            for (int i = 0; i < n; i++)
            {
                BomberEnemy nx = null; float nd = 150f;
                foreach (var e in Enemies) { if (hit.Contains(e)) continue; float d0 = Dist(e, cur); if (d0 < nd) { nd = d0; nx = e; } }
                if (nx == null) break;
                hit.Add(nx); bolt.Pts.Add(new Vector2(nx.X, nx.Y)); DamageEnemy(nx, dmg, Ang(cur, nx), 30f); cur = nx;
            }
            if (bolt.Pts.Count > 1) Bolts.Add(bolt);
        }

        void FireArc(BomberWeaponDef s)
        {
            Sound("shoot:arc");
            BomberEnemy first = null; float fd = 1e9f;
            foreach (var e in Enemies)
            {
                float d0 = Dist(e, P); if (d0 > s.Range || e.Alpha < 0.5f) continue;
                float da = Mathf.Abs(Mathf.DeltaAngle(Ang(P, e) * Mathf.Rad2Deg, P.Aim * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (da < 0.6f && d0 < fd && Los(P.X, P.Y, e.X, e.Y)) { first = e; fd = d0; }
            }
            var bolt = new BomberBolt { T = 0.15f }; bolt.Pts.Add(new Vector2(P.X, P.Y));
            if (first == null) { var end = RayEnd(P.X, P.Y, P.Aim + (_rngC.Next() - 0.5f) * 0.3f, 160f); bolt.Pts.Add(end); Bolts.Add(bolt); return; }
            var hit = new HashSet<BomberEnemy>(); BomberBody cur = P; var tgt = first;
            for (int i = 0; i <= s.Chains && tgt != null; i++)
            {
                bolt.Pts.Add(new Vector2(tgt.X, tgt.Y)); hit.Add(tgt);
                var a = Ang(cur, tgt); var nextFrom = tgt;
                DamageEnemy(tgt, s.Dmg * DmgMul, a, 40f);
                cur = nextFrom; BomberEnemy nx = null; float nd = 150f;
                foreach (var e in Enemies) { if (hit.Contains(e)) continue; float d0 = Dist(e, cur); if (d0 < nd) { nd = d0; nx = e; } }
                tgt = nx;
            }
            Bolts.Add(bolt);
        }

        // ================= projectiles =================
        BomberEnemy NearestEnemy(float x, float y, float max)
        {
            BomberEnemy best = null; float bd = max;
            foreach (var e in Enemies) { float d0 = Dist(e.X, e.Y, x, y); if (d0 < bd) { bd = d0; best = e; } }
            return best;
        }

        void UpdateShots(float dt0)
        {
            int count = Shots.Count;
            for (int bi = 0; bi < count; bi++)
            {
                var b = Shots[bi];
                if (b.Dead) continue;
                float dt = b.Enemy ? dt0 * EnemyTimeK : dt0;
                b.Life -= dt; if (b.Life <= 0f) { b.Dead = true; if (b.Aoe > 0 && !b.Enemy) Explode(b.X, b.Y, b.Aoe, b.Dmg, false, true, b.Slow); continue; }
                if (b.Homing)
                {
                    var t = NearestEnemy(b.X, b.Y, 400f);
                    if (t != null) { float cur = Mathf.Atan2(b.Vy, b.Vx), want = Mathf.Atan2(t.Y - b.Y, t.X - b.X), da = Mathf.DeltaAngle(cur * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad, na = cur + Mathf.Clamp(da, -5f * dt, 5f * dt), sp0 = Mathf.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy); b.Vx = Mathf.Cos(na) * sp0; b.Vy = Mathf.Sin(na) * sp0; }
                    if (Random.value < 0.4f) Particle(b.X, b.Y, 0, 0, 0.2f, new Color32(255, 179, 71, 136), 3f);
                }
                if (b.Trail) Particle(b.X, b.Y, 0, 0, 0.25f, new Color32(191, 244, 255, 136), 3f);
                float sp = Mathf.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy); int n = Mathf.Max(1, Mathf.CeilToInt(sp * dt / 8f));
                for (int s = 0; s < n && !b.Dead; s++)
                {
                    float ox = b.X, oy = b.Y; b.X += b.Vx * dt / n; b.Y += b.Vy * dt / n;
                    if (SolidAt(b.X, b.Y, b.R * 0.6f) || b.X < 0 || b.Y < 0 || b.X > W || b.Y > H)
                    {
                        if (b.Bounce > 0)
                        {
                            b.Bounce--; bool hx = SolidAt(b.X, oy, b.R * 0.6f), hy = SolidAt(ox, b.Y, b.R * 0.6f);
                            if (hx || !hy) b.Vx = -b.Vx; if (hy || !hx) b.Vy = -b.Vy; b.X = ox; b.Y = oy; b.Hit = null;
                        }
                        else { if (b.Aoe > 0 && !b.Enemy) Explode(b.X, b.Y, b.Aoe, b.Dmg, false, true, b.Slow); for (int k = 0; k < 4; k++) Spark(b.X, b.Y, b.Color, 120f); b.Dead = true; }
                        continue;
                    }
                    if (!b.Enemy)
                    {
                        foreach (var br in Barrels) if (br.Hp > 0f && Dist(br.X, br.Y, b.X, b.Y) < br.R + b.R) { br.Hp = 0f; _barrelQueue.Add((br, 0.12f)); if (b.Pierce <= 0) b.Dead = true; break; }
                        if (b.Dead) break;
                        BomberEnemy hitE = null;
                        foreach (var e in Enemies) { if (e.Hp <= 0f || e.Alpha < 0.5f || (b.Hit != null && b.Hit.Contains(e))) continue; if (Dist(e.X, e.Y, b.X, b.Y) < e.R + b.R) { hitE = e; break; } }
                        if (hitE == null) continue;
                        if (hitE.Kind == "shield" && Mathf.Abs(AngDiff(Mathf.Atan2(b.Y - hitE.Y, b.X - hitE.X), hitE.Face)) < 1.05f) { for (int k = 0; k < 5; k++) Spark(b.X, b.Y, new Color32(201, 139, 255, 255), 160f); b.Dead = true; break; }
                        if (b.Aoe > 0) { Explode(b.X, b.Y, b.Aoe, b.Dmg, false, true, b.Slow); b.Dead = true; break; }
                        DamageEnemy(hitE, b.Dmg, Mathf.Atan2(b.Vy, b.Vx), b.Kb, false, b.Burn, b.Slow);
                        if (b.Zap) ChainZap(hitE, b.Dmg * 0.3f, 1);
                        if (b.Split) { float a0 = Mathf.Atan2(b.Vy, b.Vx); foreach (var o in new[] { -0.5f, 0.5f }) Shot(b.X, b.Y, a0 + o, sp * 0.8f, b.Dmg * 0.5f, new BomberShot { R = Mathf.Max(2f, b.R * 0.7f), Hit = new HashSet<BomberEnemy> { hitE }, Life = 0.4f, Color = b.Color, Split = false }); Shots[Shots.Count - 1].Split = false; Shots[Shots.Count - 2].Split = false; }
                        if (b.Pierce > 0) { b.Pierce--; (b.Hit ??= new HashSet<BomberEnemy>()).Add(hitE); } else b.Dead = true;
                    }
                    else
                    {
                        if (HasMod("reflect") && P.DashT > 0f && Dist(P.X, P.Y, b.X, b.Y) < P.R + b.R + 8f)
                        {
                            float s2 = Mathf.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy) * 1.2f;
                            b.Enemy = false; b.Dmg = 15f; b.Color = new Color32(191, 244, 255, 255); b.Life = 1.2f; b.Vx = Mathf.Cos(P.Aim) * s2; b.Vy = Mathf.Sin(P.Aim) * s2; b.Hit = null; break;
                        }
                        if (P.BubbleT > 0f && Dist(P.X, P.Y, b.X, b.Y) < 46f) { b.Dead = true; Spark(b.X, b.Y, new Color32(159, 208, 255, 255), 100f); break; }
                        if (Dist(P.X, P.Y, b.X, b.Y) < P.R * 0.75f + b.R && P.ITime <= 0f) { Hurt(); b.Dead = true; }
                    }
                }
            }
            Shots.RemoveAll(x => x.Dead);
        }

        // ================= companions, trails, bolts =================
        void UpdateCompanions(float dt)
        {
            for (int i = 0; i < P.Comps.Count; i++)
            {
                var c = P.Comps[i];
                if (c.Id == "guard")
                {
                    c.A += dt * 3.2f; c.X = P.X + Mathf.Cos(c.A) * 24f; c.Y = P.Y + Mathf.Sin(c.A) * 24f;
                    foreach (var b in Shots) if (b.Enemy && !b.Dead && Dist(b.X, b.Y, c.X, c.Y) < b.R + 8f) { b.Dead = true; Spark(b.X, b.Y, new Color32(159, 208, 255, 255), 120f); }
                    continue;
                }
                if (c.Id == "striker" && c.St == "dive")
                {
                    if (!Enemies.Contains(c.Tgt)) { c.St = "back"; continue; }
                    float a = Mathf.Atan2(c.Tgt.Y - c.Y, c.Tgt.X - c.X); c.X += Mathf.Cos(a) * 520f * dt; c.Y += Mathf.Sin(a) * 520f * dt;
                    if (Dist(c.X, c.Y, c.Tgt.X, c.Tgt.Y) < c.Tgt.R + 6f) { DamageEnemy(c.Tgt, 15f, a, 160f); c.St = "back"; }
                    continue;
                }
                c.A += dt * 1.4f; float tx = P.X + Mathf.Cos(c.A + i * Mathf.PI) * 38f, ty = P.Y + Mathf.Sin(c.A + i * Mathf.PI) * 38f, kk = Mathf.Min(1f, dt * (c.St == "back" ? 6f : 10f));
                c.X += (tx - c.X) * kk; c.Y += (ty - c.Y) * kk;
                if (c.St == "back" && Dist(tx, ty, c.X, c.Y) < 10f) c.St = "orbit";
                c.Cd -= dt;
                if (c.Cd <= 0f && Enemies.Count > 0)
                {
                    BomberEnemy best = null; float bd = c.Id == "striker" ? 280f : 320f;
                    foreach (var e in Enemies) { if (e.Alpha < 0.5f) continue; float d0 = Dist(e.X, e.Y, c.X, c.Y); if (d0 < bd && Los(c.X, c.Y, e.X, e.Y)) { bd = d0; best = e; } }
                    if (best != null)
                    {
                        if (c.Id == "gunner") { c.Cd = 0.5f; Shot(c.X, c.Y, Mathf.Atan2(best.Y - c.Y, best.X - c.X), 620f, 6f, new BomberShot { R = 3, Life = 0.8f, Color = new Color32(191, 244, 255, 255) }); Shots[Shots.Count - 1].Split = false; }
                        if (c.Id == "striker" && c.St == "orbit") { c.Cd = 2f; c.St = "dive"; c.Tgt = best; }
                    }
                }
            }
            for (int i = Trails.Count - 1; i >= 0; i--)
            {
                var t = Trails[i]; t.T -= dt; if (t.T <= 0f) { Trails.RemoveAt(i); continue; }
                foreach (var e in Enemies.ToArray()) if (!t.Hit.Contains(e) && Dist(t.X, t.Y, e.X, e.Y) < e.R + 10f) { t.Hit.Add(e); DamageEnemy(e, 12f); }
            }
            for (int i = Bolts.Count - 1; i >= 0; i--) { Bolts[i].T -= dt; if (Bolts[i].T <= 0f) Bolts.RemoveAt(i); }
            if (HasMod("shieldm") && P.Shield <= 0f) { P.ShieldT -= dt; if (P.ShieldT <= 0f) P.Shield = 1f; }
        }

        // ================= loot =================
        List<BomberItem> LootOptions(int n, params string[] kinds)
        {
            var pool = new List<BomberItem>();
            if (System.Array.IndexOf(kinds, "weapon") >= 0) foreach (var id in BomberItems.BaseWeapons) if (Meta.WeaponUnlocked(id) && !P.Weapons.Exists(w => w.Id == id)) pool.Add(new BomberItem { Type = "weapon", Id = id });
            if (System.Array.IndexOf(kinds, "module") >= 0) foreach (var id in BomberItems.Mods.Keys) if (!HasMod(id)) pool.Add(new BomberItem { Type = "module", Id = id });
            if (System.Array.IndexOf(kinds, "companion") >= 0) foreach (var id in BomberItems.Comps.Keys) if (Meta.CompUnlocked(id) && !HasComp(id)) pool.Add(new BomberItem { Type = "companion", Id = id });
            _rngLoot.Shuffle(pool);
            return pool.GetRange(0, Mathf.Min(n, pool.Count));
        }

        void OfferReward(string title, string sub, params string[] kinds)
        {
            var opts = LootOptions(Meta.Up("luck") > 0 ? 4 : 3, kinds);
            if (opts.Count == 0) return;
            Reward = new BomberReward { Title = title, Sub = sub }; Reward.Options.AddRange(opts);
        }

        /// <summary>The player picked option i of the current reward.</summary>
        public void ChooseReward(int i)
        {
            if (Reward == null || i < 0 || i >= Reward.Options.Count) return;
            var it = Reward.Options[i]; Reward = null; Grant(it);
        }

        /// <summary>What happens to the player's slots if this item is taken (shown on reward cards and in the shop).</summary>
        public string SlotInfo(BomberItem it)
        {
            if (it.Type == "weapon")
            {
                if (P.Weapons.Count < 2) return "Слот оружия 2/2 свободен";
                var cw = P.Weapon; return $"Заменит: {cw.Def.Name}" + (cw.UpSum > 0 ? $" (пропадут ▲{cw.UpSum})" : "") + ". Смени оружие, чтобы заменить другое";
            }
            if (it.Type == "module")
            {
                var cat = BomberItems.Mods[it.Id].Cat; int same = P.Mods.FindAll(m => BomberItems.Mods[m].Cat == cat).Count;
                var s = P.Mods.Count < P.ModSlots ? $"Слот модуля свободен ({P.Mods.Count}/{P.ModSlots})" : $"Слоты заняты: снимется {BomberItems.Mods[P.Mods[0]].Name}";
                return same == 1 ? s + ". Включит бонус набора «" + cat + "»" : s;
            }
            if (it.Type == "companion") return P.Comps.Count < P.CompSlots ? "Слот компаньона свободен" : $"Улетит: {BomberItems.Comps[P.Comps[0].Id].Name}";
            return P.Hp < P.MaxHp ? $"Здоровье {P.Hp}/{P.MaxHp}" : "Здоровье уже полное";
        }

        void Grant(BomberItem it)
        {
            if (it.Type == "repair") { P.Hp = Mathf.Min(P.MaxHp, P.Hp + 1); Toast?.Invoke("+1 здоровье", 1.2f); return; }
            if (it.Type == "weapon")
            {
                if (P.Weapons.Count < 2) { P.Weapons.Add(new BomberWeapon { Id = it.Id }); P.Cur = P.Weapons.Count - 1; }
                else { Toast?.Invoke($"{P.Weapon.Def.Name} заменён на {BomberItems.Weapons[it.Id].Name}", 2f); P.Weapons[P.Cur] = new BomberWeapon { Id = it.Id }; }
                var f = FusionFor();
                if (f.id != null) Toast?.Invoke($"Можно слить: {BomberItems.Weapons[f.a].Name} + {BomberItems.Weapons[f.b].Name} → {BomberItems.Weapons[f.id].Name}. Прокачай оба до ▲{BomberItems.FuseNeed} на верстаке.", 4f);
                return;
            }
            if (it.Type == "companion")
            {
                if (P.Comps.Count >= P.CompSlots) { Toast?.Invoke($"Слот занят: {BomberItems.Comps[P.Comps[0].Id].Name} улетел", 1.6f); P.Comps.RemoveAt(0); }
                P.Comps.Add(new BomberComp { Id = it.Id, A = _rngC.Next() * 6.28f, X = P.X, Y = P.Y });
                return;
            }
            var before = new HashSet<string>(); foreach (var c in BomberItems.Sets.Keys) if (SetOn(c)) before.Add(c);
            if (P.Mods.Count >= P.ModSlots) { Toast?.Invoke($"Слоты заняты: {BomberItems.Mods[P.Mods[0]].Name} снят", 1.6f); P.Mods.RemoveAt(0); }
            P.Mods.Add(it.Id);
            foreach (var c in BomberItems.Sets.Keys) if (SetOn(c) && !before.Contains(c)) Toast?.Invoke(BomberItems.Sets[c], 3f);
            ApplyCoreMods(); RecalcHp();
        }

        // ---- hooks for EditMode tests ----
        public void EnterRoomForTests(string type) => EnterRoom(type);
        public void GrantForTests(BomberItem it) => Grant(it);

        public void SwapWeapon() { if (P.Weapons.Count > 1) { P.Cur = (P.Cur + 1) % P.Weapons.Count; P.Beam.On = false; } }

        // ================= rooms with props =================
        void MakeShop()
        {
            int n = Meta.IsRescued("gunsmith") ? 4 : 3; var items = LootOptions(n, "weapon", "module", "companion");
            for (int i = 0; i <= items.Count; i++)
            {
                float x = W / 2f + (i % 2 == 1 ? 90f : -90f), y = 190f + (i / 2) * 150f;
                if (i == items.Count) { Props.Add(new BomberProp { Kind = "shop", X = x, Y = y, Item = new BomberItem { Type = "repair" }, Price = 30 }); break; }
                var it = items[i];
                int price = it.Type == "weapon" ? 65 + _rngLoot.Range(26) : it.Type == "companion" ? 70 + _rngLoot.Range(21) : 40 + _rngLoot.Range(31);
                Props.Add(new BomberProp { Kind = "shop", X = x, Y = y, Item = it, Price = price });
            }
            Props.Add(new BomberProp { Kind = "bench", X = W / 2f, Y = H - 250f });
        }

        public BomberProp NearProp()
        {
            BomberProp best = null; float bd = 56f;
            foreach (var p in Props) { if (p.Opened || p.Sold) continue; float d0 = Dist(p.X, p.Y, P.X, P.Y); if (d0 < bd) { best = p; bd = d0; } }
            return best;
        }

        void Interact()
        {
            var p = NearProp(); var npc = NearNpc();
            if (npc != null && (p == null || Dist(npc.X, npc.Y, P.X, P.Y) < Dist(p.X, p.Y, P.X, P.Y))) { PendingPanel = "npc:" + npc.Id; return; }
            if (p == null) return;
            switch (p.Kind)
            {
                case "station": PendingPanel = p.Id; break;
                case "cell": if (Room.Cleared) Rescue(p); else Toast?.Invoke("Сначала зачисти комнату — замок камеры откроется", 1.8f); break;
                case "chest": p.Opened = true; OfferReward("Сундук снабжения", "Выбери один предмет.", "weapon", "module", "companion"); break;
                case "altar":
                    if (P.MaxHp <= 2) { Toast?.Invoke("Алтарю нужна жертва, а у тебя слишком мало корпуса", 2f); return; }
                    p.Opened = true; P.AltarLoss++; RecalcHp(); Shake?.Invoke(0.5f); Flash = 0.15f;
                    OfferReward("Алтарь риска", "−1 к максимальному здоровью. Выбери награду.", "weapon", "module", "companion"); break;
                case "bench": BenchOpen = true; break;
                case "shop":
                    int pr = PriceOf(p);
                    if (p.Item.Type == "repair" && P.Hp >= P.MaxHp) { Toast?.Invoke("Здоровье полное", 1.2f); return; }
                    if (P.Scrap < pr) { Toast?.Invoke($"Нужно {pr} скрапа, у тебя {P.Scrap}", 1.6f); return; }
                    P.Scrap -= pr; p.Sold = true; Sound("buy"); Grant(p.Item); if (p.Item.Type != "repair") Toast?.Invoke($"Куплено: {p.Item.Name}", 1.4f);
                    break;
            }
        }

        // ================= workbench =================
        public void CloseBench() => BenchOpen = false;

        public int UpgradePrice(BomberWeapon w, string key)
        {
            var u = System.Array.Find(BomberItems.Upgrades, x => x.id == key); int l = w.Up[key];
            return l >= 3 ? 0 : Mathf.RoundToInt(u.cost[l] * Discount * (Room.BenchFree ? 0.5f : 1f) * (Meta.IsRescued("mechanic") ? 0.8f : 1f));
        }

        public bool BuyUpgrade(int wi, string key)
        {
            if (wi < 0 || wi >= P.Weapons.Count) return false;
            var w = P.Weapons[wi]; if (w.Up[key] >= 3) return false;
            int price = UpgradePrice(w, key); if (P.Scrap < price) return false;
            P.Scrap -= price; w.Up[key]++; Room.BenchFree = false; return true;
        }

        public (string id, string a, string b) FusionFor()
        {
            foreach (var f in BomberItems.Fusions) if (P.Weapons.Exists(w => w.Id == f.a) && P.Weapons.Exists(w => w.Id == f.b)) return f;
            return (null, null, null);
        }

        public int FusePrice => Mathf.RoundToInt(BomberItems.FuseCost * Discount);

        public bool Fuse()
        {
            var f = FusionFor(); if (f.id == null) return false;
            var wa = P.Weapons.Find(w => w.Id == f.a); var wb = P.Weapons.Find(w => w.Id == f.b);
            if (wa.UpSum < BomberItems.FuseNeed || wb.UpSum < BomberItems.FuseNeed || P.Scrap < FusePrice) return false;
            P.Scrap -= FusePrice; Sound("ach");
            var nw = new BomberWeapon { Id = f.id };
            foreach (var k in new[] { "dmg", "rate", "heat", "spec" }) nw.Up[k] = Mathf.Max(wa.Up[k], wb.Up[k]);
            P.Weapons.Clear(); P.Weapons.Add(nw); P.Cur = 0;
            Shake?.Invoke(0.6f); for (int i = 0; i < 40; i++) Spark(P.X, P.Y, new Color32(255, 214, 92, 255), 300f);
            Toast?.Invoke($"Слияние: {nw.Def.Name}! Второй слот свободен.", 3f);
            if (Mode == BomberMode.Run) { Meta.Fusions++; Unlock("fusion"); SaveMeta(); }
            return true;
        }
    }
}
