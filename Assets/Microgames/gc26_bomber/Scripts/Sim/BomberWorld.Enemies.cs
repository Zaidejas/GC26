using UnityEngine;

namespace GC26.Bomber
{
    // Enemies, projectiles, damage and explosions — ported from the prototype (updateEnemy, updateBullets, killEnemy, explode).
    public sealed partial class BomberWorld
    {
        BomberEnemy MakeEnemy(string kind, float x, float y, bool elite)
        {
            var d = BomberData.Enemies[kind]; float hp = d.Hp * HpMul * (elite ? 1.5f : 1f);
            var e = new BomberEnemy
            {
                Kind = kind, Def = d, X = x, Y = y, R = d.R + (elite ? 2f : 0f), Hp = hp, MaxHp = hp, Elite = elite, Tm = 0.4f + _rngC.Next() * 0.8f,
                Orbit = _rngC.Next() < 0.5f ? 1f : -1f, ShootT = 2f + _rngC.Next() * 1.5f, SpdMul = BomberData.EnemySpeed, Wave = Room != null ? Room.Wave : 0,
            };
            if (elite)
            {
                e.Affix = _rngE.Pick(BomberData.AffixKeys);
                if (e.Affix == "fast") e.SpdMul *= 1.35f;
                if (e.Affix == "shielded") e.ShieldHp = hp * 0.5f;
            }
            return e;
        }

        static float AngDiff(float a, float b) => Mathf.Repeat(a - b + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        void EnemyShot(float x, float y, float a, float speed, Color32? color = null, float r = 5f)
        {
            float s = speed * BomberData.BulletMul * (Is("chrono") ? 0.88f : 1f) * (1f + 0.06f * Ol);
            Shots.Add(new BomberShot { X = x, Y = y, Vx = Mathf.Cos(a) * s, Vy = Mathf.Sin(a) * s, Enemy = true, Dmg = 1, R = r, Life = 5f, Color = color ?? new Color32(255, 79, 106, 255) });
        }

        Vector2 FlowDir(BomberEnemy e)
        {
            int cx = Mathf.Clamp((int)(e.X / Cell), 0, GW - 1), cy = Mathf.Clamp((int)(e.Y / Cell), 0, GH - 1);
            int best = _flow[cy * GW + cx]; if (best < 0) best = int.MaxValue; float bx = 0, by = 0; bool found = false;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= GW || ny >= GH) continue;
                int n = ny * GW + nx; if (_blocked[n] != 0 || _flow[n] < 0) continue;
                if (dx != 0 && dy != 0 && (_blocked[cy * GW + nx] != 0 || _blocked[ny * GW + cx] != 0)) continue;
                if (_flow[n] < best) { best = _flow[n]; bx = nx * Cell + 16; by = ny * Cell + 16; found = true; }
            }
            float a = found ? Mathf.Atan2(by - e.Y, bx - e.X) : Ang(e, P);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        Vector2 Toward(BomberEnemy e) => Cloaked ? Wander(e) : e.Los ? new Vector2(Mathf.Cos(Ang(e, P)), Mathf.Sin(Ang(e, P))) : FlowDir(e);

        Vector2 KeepAt(BomberEnemy e, float d, float a, float near, float far)
        {
            int rad = d < near ? -1 : d > far ? 1 : 0;
            if (rad == 1) return Toward(e);
            if (rad == -1) return new Vector2(-Mathf.Cos(a), -Mathf.Sin(a));
            return new Vector2(-Mathf.Sin(a) * e.Orbit * 0.5f, Mathf.Cos(a) * e.Orbit * 0.5f);
        }

        void Steer(BomberEnemy e, Vector2 dir, float speed, float dt)
        {
            speed *= e.SpdMul; if (e.Slow > 0f) speed *= 0.5f;
            float sx = 0, sy = 0;
            foreach (var o in Enemies) { if (o == e) continue; float d0 = Dist(e, o), m = e.R + o.R + 6f; if (d0 < m && d0 > 0f) { sx += (e.X - o.X) / d0 * (m - d0) / m; sy += (e.Y - o.Y) / d0 * (m - d0) / m; } }
            float tx = dir.x * speed + sx * 120f, ty = dir.y * speed + sy * 120f, k = Mathf.Min(1f, dt * 8f);
            e.Vx += (tx - e.Vx) * k; e.Vy += (ty - e.Vy) * k;
            MoveBody(e, e.Vx * dt, e.Vy * dt);
        }

        void UpdateEnemy(BomberEnemy e, float dt)
        {
            e.Flash -= dt; if (e.Slow > 0f) e.Slow -= dt;
            if (e.Affix == "regen" && e.Hp > 0f) e.Hp = Mathf.Min(e.MaxHp, e.Hp + e.MaxHp * 0.04f * dt);
            if (e.Burn > 0f) { e.Burn -= dt; e.BurnT -= dt; if (e.BurnT <= 0f) { e.BurnT = 0.33f; DamageEnemy(e, 1f, 0f, 0f, true); if (e.Hp <= 0f) return; } }
            e.LosT -= dt; if (e.LosT <= 0f) { e.LosT = 0.2f; e.Los = !Cloaked && Los(e.X, e.Y, P.X, P.Y); }
            if (e.Stun > 0f) { e.Stun -= dt; e.Vx *= 0.8f; e.Vy *= 0.8f; MoveBody(e, e.Vx * dt, e.Vy * dt); return; }
            if (e.IsBoss) { UpdateBoss(e, dt); return; }
            float d = Dist(e, P), a = Ang(e, P), spd = e.Def.Speed;
            float el = (e.Elite ? 0.75f : 1f) * (e.Affix == "fast" ? 0.8f : 1f) * BomberData.Interval * (e.Slow > 0f ? 1.5f : 1f) / (1f + 0.05f * Ol);
            e.Tm -= dt;
            switch (e.Kind)
            {
                case "beetle": case "mini":
                    if (e.St == "idle" || e.St == "chase") { e.St = "chase"; Steer(e, Toward(e), spd * (e.Elite ? 1.15f : 1f), dt); if (d < e.R + P.R + 14f) { e.St = "tele"; e.Tm = 0.38f; } }
                    else if (e.St == "tele") { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) { if (d < e.R + P.R + 24f) Hurt(); e.Vx = Mathf.Cos(a) * 260f; e.Vy = Mathf.Sin(a) * 260f; e.St = "rec"; e.Tm = 0.55f * el; } }
                    else if (e.St == "rec") { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) e.St = "chase"; }
                    break;
                case "turret":
                    e.Vx = e.Vy = 0f;
                    if (e.St == "idle") { e.Face = a; if (e.Tm <= 0f && e.Los) { e.St = "tele"; e.Tm = 0.45f; } }
                    else if (e.St == "tele") { e.Face = a; if (e.Tm <= 0f) { e.St = "fire"; e.Cnt = e.Elite ? 5 : 3; e.Tm = 0f; e.AimA = e.Face; } }
                    else if (e.St == "fire") { if (e.Tm <= 0f) { EnemyShot(e.X + Mathf.Cos(e.AimA) * 14f, e.Y + Mathf.Sin(e.AimA) * 14f, e.AimA, 260f); e.Cnt--; e.Tm = 0.11f; if (e.Cnt <= 0) { e.St = "idle"; e.Tm = 1.2f * el; } } }
                    break;
                case "scout":
                {
                    const float want = 180f; int rad = d > want + 30f ? 1 : d < want - 30f ? -1 : 0;
                    float tx = Mathf.Cos(a) * rad - Mathf.Sin(a) * e.Orbit * 0.9f, ty = Mathf.Sin(a) * rad + Mathf.Cos(a) * e.Orbit * 0.9f, n = Mathf.Max(0.0001f, Mathf.Sqrt(tx * tx + ty * ty));
                    Steer(e, e.Los ? new Vector2(tx / n, ty / n) : FlowDir(e), spd, dt);
                    if (SolidAt(e.X + e.Vx * 0.1f, e.Y + e.Vy * 0.1f, e.R)) e.Orbit *= -1f;
                    if (e.St == "idle") { if (e.Tm <= 0f && e.Los) { e.St = "tele"; e.Tm = 0.38f; } }
                    else if (e.St == "tele") { if (e.Tm <= 0f) { if (e.Elite) foreach (var o in new[] { -0.25f, 0f, 0.25f }) EnemyShot(e.X, e.Y, a + o, 240f); else EnemyShot(e.X, e.Y, a, 250f); e.St = "idle"; e.Tm = 1.5f * el; } }
                    break;
                }
                case "kamikaze":
                    if (e.St != "tele") { Steer(e, Toward(e), spd, dt); if (d < 62f) { e.St = "tele"; e.Tm = 0.55f; } }
                    else { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) { Explode(e.X, e.Y, 80f, 20f, true, false); if (e.Elite) for (int i = 0; i < 8; i++) EnemyShot(e.X, e.Y, i * Mathf.PI / 4f, 200f); e.Hp = 0f; KillEnemy(e); return; } }
                    break;
                case "splitter":
                    Steer(e, KeepAt(e, d, a, 140f, 240f), spd, dt);
                    if (e.St == "idle") { if (e.Tm <= 0f && e.Los) { e.St = "tele"; e.Tm = 0.4f; } }
                    else if (e.St == "tele") { if (e.Tm <= 0f) { foreach (var o in new[] { -0.12f, 0.12f }) EnemyShot(e.X, e.Y, a + o, 230f, new Color32(127, 209, 255, 255)); e.St = "idle"; e.Tm = 2f * el; } }
                    break;
                // ---- sector 2 ----
                case "shield":
                {
                    e.Face += Mathf.Clamp(AngDiff(a, e.Face), -1.5f * dt, 1.5f * dt);
                    if (e.St == "tele") { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) { if (d < e.R + P.R + 24f) Hurt(); e.St = "rec"; e.Tm = 0.7f; } }
                    else if (e.St == "rec") { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) e.St = "chase"; }
                    else { e.St = "chase"; Steer(e, Toward(e), spd, dt); if (d < e.R + P.R + 14f) { e.St = "tele"; e.Tm = 0.45f; } }
                    e.ShootT -= dt;
                    if (e.ShootT <= 0f && e.Los && e.St == "chase") { e.ShootT = 2.8f * el; int n = e.Elite ? 5 : 3; for (int i = 0; i < n; i++) EnemyShot(e.X, e.Y, e.Face + (i - (n - 1) / 2f) * 0.22f, 210f, Orange); }
                    break;
                }
                case "sniper":
                    if (e.St == "aim" || e.St == "shot") Steer(e, Vector2.zero, 0, dt);
                    else { Steer(e, KeepAt(e, d, a, 280f, 380f), spd, dt); if (SolidAt(e.X + e.Vx * 0.15f, e.Y + e.Vy * 0.15f, e.R)) e.Orbit *= -1f; }
                    if (e.St == "idle") { if (e.Tm <= 0f && e.Los) { e.St = "aim"; e.Tm = 1f; e.AimA = a; } }
                    else if (e.St == "aim") { if (e.Tm > 0.3f) e.AimA = a; if (e.Tm <= 0f) { EnemyShot(e.X, e.Y, e.AimA, 900f, null, 4f); if (e.Elite) { e.St = "shot"; e.Tm = 0.15f; } else { e.St = "idle"; e.Tm = 2.1f * el; } } }
                    else if (e.St == "shot") { if (e.Tm <= 0f) { EnemyShot(e.X, e.Y, e.AimA, 900f, null, 4f); e.St = "idle"; e.Tm = 2.1f * el; } }
                    break;
                case "mortar":
                    Steer(e, KeepAt(e, d, a, 260f, 400f), spd, dt);
                    if (e.Tm <= 0f && !Cloaked)
                    {
                        int n = e.Elite ? 3 : 1;
                        for (int i = 0; i < n; i++)
                        {
                            float tx = Mathf.Clamp(P.X + (i > 0 ? (_rngC.Next() - 0.5f) * 140f : P.Vx * 0.4f), 40f, W - 40f), ty = Mathf.Clamp(P.Y + (i > 0 ? (_rngC.Next() - 0.5f) * 140f : P.Vy * 0.4f), 40f, H - 40f);
                            Effects.Add(new BomberEffect { Kind = "shell", X = tx, Y = ty, T = 1.05f, Max = 1.05f, R = 46f });
                        }
                        e.Tm = 2.8f * el;
                    }
                    break;
                case "brute":
                    if (e.St == "charge")
                    {
                        float ox = e.X, oy = e.Y, sp = 470f * dt * e.SpdMul; MoveBody(e, Mathf.Cos(e.AimA) * sp, Mathf.Sin(e.AimA) * sp); if (Dist(e, P) < e.R + P.R + 2f) Hurt();
                        if (e.Tm <= 0f || Dist(e.X, e.Y, ox, oy) < sp * 0.4f) { e.St = "rec"; e.Tm = 0.8f; Shake?.Invoke(0.4f); for (int k = 0; k < 10; k++) Spark(e.X, e.Y, new Color32(255, 107, 61, 255), 200f); }
                        break;
                    }
                    if (e.St == "tele") { Steer(e, Vector2.zero, 0, dt); if (e.Tm > 0.25f) e.AimA = a; if (e.Tm <= 0f) { e.St = "charge"; e.Tm = 0.55f; } break; }
                    if (e.St == "rec") { Steer(e, Vector2.zero, 0, dt); if (e.Tm <= 0f) { e.St = "chase"; e.Tm = 2.2f * el; } break; }
                    e.St = "chase"; Steer(e, Toward(e), spd, dt);
                    if (e.Los && d < 280f && e.Tm <= 0f) { e.St = "tele"; e.Tm = 0.7f; e.AimA = a; }
                    e.ShootT -= dt; if (e.ShootT <= 0f && e.Los) { e.ShootT = 3f * el; for (int i = 0; i < 7; i++) EnemyShot(e.X, e.Y, a + (i - 3) * 0.12f, 230f, Orange); }
                    break;
                // ---- sector 3 ----
                case "repair":
                {
                    BomberEnemy target = null, anchor = null;
                    foreach (var o in Enemies) { if (o == e || o.IsBoss) continue; if (anchor == null) anchor = o; if (o.Hp < o.MaxHp && (target == null || o.Hp / o.MaxHp < target.Hp / target.MaxHp)) target = o; }
                    if (target != null) anchor = target;
                    Vector2 dir;
                    if (d < 220f) dir = new Vector2(-Mathf.Cos(a), -Mathf.Sin(a));
                    else if (anchor != null && Dist(e, anchor) > 120f) { float aa = Ang(e, anchor); dir = new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)); }
                    else dir = Vector2.zero;
                    Steer(e, dir, spd, dt);
                    if (e.St == "idle") { if (e.Tm <= 0f && target != null && Dist(e, target) < 260f) { e.St = "tele"; e.Tm = 0.4f; e.Tgt = target; } }
                    else if (e.St == "tele" && e.Tm <= 0f)
                    {
                        Heal(e, e.Tgt);
                        if (e.Elite) foreach (var o in Enemies) if (o != e && o != e.Tgt && !o.IsBoss && o.Hp < o.MaxHp) { Heal(e, o); break; }
                        e.St = "idle"; e.Tm = 2f * el; e.Tgt = null;
                    }
                    break;
                }
                case "phantom":
                    if (e.St == "out")
                    {
                        e.Alpha = Mathf.Max(0.08f, e.Tm / 0.35f); Steer(e, Vector2.zero, 0, dt);
                        if (e.Tm <= 0f)
                        {
                            float aa = _rngC.Next() * Mathf.PI * 2f; e.X = Mathf.Clamp(P.X + Mathf.Cos(aa) * 72f, 40f, W - 40f); e.Y = Mathf.Clamp(P.Y + Mathf.Sin(aa) * 72f, 40f, H - 40f); PushOut(e);
                            e.St = "slash"; e.Tm = 0.42f; e.Alpha = 1f; for (int k = 0; k < 10; k++) Spark(e.X, e.Y, new Color32(217, 140, 255, 255), 160f);
                        }
                        break;
                    }
                    if (e.St == "slash")
                    {
                        Steer(e, Vector2.zero, 0, dt);
                        if (e.Tm <= 0f) { if (Dist(e, P) < e.R + P.R + 28f) Hurt(); float sa = Ang(e, P); for (int k = 0; k < 8; k++) Spark(e.X + Mathf.Cos(sa) * 20f, e.Y + Mathf.Sin(sa) * 20f, new Color32(255, 255, 255, 255), 200f); e.St = "chase"; e.Tm = 3f * el; }
                        break;
                    }
                    e.St = "chase"; e.Alpha = 1f; Steer(e, Toward(e), spd, dt);
                    if (e.Tm <= 0f && d < 460f && !Cloaked) { e.St = "out"; e.Tm = 0.35f; }
                    break;
                case "laser":
                    e.Vx = e.Vy = 0f;
                    if (e.St == "idle") { e.Face = a; if (e.Tm <= 0f && e.Los) { e.St = "tele"; e.Tm = 1f; e.AimA = a - 0.55f * e.Orbit; } }
                    else if (e.St == "tele") { if (e.Tm <= 0f) { e.St = "sweep"; e.Tm = 1.4f; } }
                    else if (e.St == "sweep")
                    {
                        e.AimA += dt * 0.85f * e.Orbit * (e.Elite ? 1.4f : 1f);
                        var end = RayEnd(e.X, e.Y, e.AimA, 900f); if (SegDist(P.X, P.Y, e.X, e.Y, end.x, end.y) < P.R + 4f && P.DashT <= 0f) Hurt();
                        if (e.Tm <= 0f) { e.St = "idle"; e.Tm = 2.4f * el; e.Orbit *= -1f; }
                    }
                    break;
            }
        }

        static readonly Color32 Orange = new Color32(255, 154, 92, 255);

        void Heal(BomberEnemy from, BomberEnemy t)
        {
            if (t == null || !Enemies.Contains(t)) return;
            t.Hp = Mathf.Min(t.MaxHp, t.Hp + t.MaxHp * 0.35f);
            Effects.Add(new BomberEffect { Kind = "beam", X = from.X, Y = from.Y, X2 = t.X, Y2 = t.Y, T = 0.3f, Max = 0.3f, Color = new Color32(125, 255, 168, 255) });
            Texts.Add(new BomberText { X = t.X, Y = t.Y - 12f, S = "+", T = 0.7f, Color = new Color32(109, 255, 176, 255) });
        }

        /// <summary>Enemies that cannot be knocked back.</summary>
        static bool Anchored(BomberEnemy e) => e.Kind == "turret" || e.Kind == "laser" || e.IsBoss;

        /// <param name="burnTick">damage from burning (no number, no flash)</param>
        /// <param name="ignite">sets the enemy on fire (or the Glue module does)</param>
        public void DamageEnemy(BomberEnemy e, float amt, float a = 0f, float kb = 0f, bool burnTick = false, bool ignite = false, float slow = 0f)
        {
            if (e.Hp <= 0f) return;
            if (e.Affix == "armored") amt *= 0.6f;
            if (Is("whisper") && (e.Elite || e.IsBoss)) amt *= 1.25f;
            if (e.ShieldHp > 0f) { float ab = Mathf.Min(e.ShieldHp, amt); e.ShieldHp -= ab; amt -= ab; if (!burnTick) Spark(e.X, e.Y, new Color32(159, 208, 255, 255), 120f); }
            e.Hp -= amt; if (!burnTick) e.Flash = 0.08f;
            if (kb > 0f && !Anchored(e)) { e.Vx += Mathf.Cos(a) * kb; e.Vy += Mathf.Sin(a) * kb; }
            if (!burnTick && (ignite || HasMod("glue"))) e.Burn = 3f;
            if (slow > 0f) e.Slow = Mathf.Max(e.Slow, slow);
            if (burnTick) { if (e.Hp <= 0f) KillEnemy(e); return; }
            bool crit = amt >= 40f;
            Texts.Add(new BomberText { X = e.X + (_rngC.Next() - 0.5f) * 10f, Y = e.Y - e.R - 4f, S = Mathf.RoundToInt(amt).ToString(), T = crit ? 0.8f : 0.55f, Color = crit ? new Color32(255, 214, 92, 255) : Core.State == CoreState.Overdrive ? new Color32(255, 227, 154, 255) : new Color32(219, 230, 255, 255) });
            if (crit) Shake?.Invoke(0.3f);
            if (e.Hp <= 0f) KillEnemy(e);
        }

        void KillEnemy(BomberEnemy e)
        {
            if (!Enemies.Remove(e)) return;
            for (int k = 0; k < 16; k++) Spark(e.X, e.Y, e.Def.Color, 260f);
            AddRing(e.X, e.Y, e.R + 16f, e.Def.Color, 0.25f);
            NewDecals.Add((e.X, e.Y, e.R, e.Def.Color));
            if (e.IsBoss) { BossDefeated(e); return; }
            if (e.Affix == "volatile") Effects.Add(new BomberEffect { Kind = "mine", X = e.X, Y = e.Y, T = 0.5f, Max = 0.5f, R = 70f });
            Kills++; Combo++; ComboT = 2.6f; MaxCombo = Mathf.Max(MaxCombo, Combo);
            if (Mode == BomberMode.Run) { Meta.Kills++; if (Meta.Kills >= 300) Unlock("kills300"); if (Combo >= 25) Unlock("combo25"); QProgress("kill", e.Kind); if (e.Elite) QProgress("elite", "any"); }
            if (Is("leech") && ++P.LeechK % 12 == 0 && P.Hp < P.MaxHp) { P.Hp++; Texts.Add(new BomberText { X = P.X, Y = P.Y - 26, S = "+1", T = 0.9f, Color = new Color32(255, 77, 141, 255) }); }
            if (Combo % 5 == 0)
            {
                for (int k = 0; k < 3; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = e.X + (_rngC.Next() - 0.5f) * 20f, Y = e.Y + (_rngC.Next() - 0.5f) * 20f });
                Texts.Add(new BomberText { X = e.X, Y = e.Y - 24, S = $"КОМБО ×{Combo}", T = 0.9f, Color = new Color32(255, 214, 92, 255) });
            }
            if (Combo == 10) Toast?.Invoke("В УДАРЕ: +10% скорости, пока держишь комбо", 1.8f);
            Score += Mathf.RoundToInt((e.Elite ? 50 : 10) * (1f + Mathf.Min(Combo, 20) * 0.05f));
            if (e.Kind == "splitter") foreach (var o in new[] { -1f, 1f }) { var m = MakeEnemy("mini", e.X + o * 10f, e.Y, false); m.Wave = e.Wave; m.Tm = 0.4f; Enemies.Add(m); }
            if (e.Burn > 0f && HasMod("napalm")) Explode(e.X, e.Y, 60f, 15f, false, true);
            if (e.Burn > 0f && SetOn("Огонь")) foreach (var o in Enemies) if (Dist(o, e) < 80f) o.Burn = 3f;
            if (e.Kind == "mini") return;
            int n = 1 + _rngLoot.Range(3) + (e.Elite ? 4 : 0) + (SetOn("Экономика") ? 1 : 0) + (Is("spark") ? 1 : 0);
            for (int k = 0; k < n; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = e.X + (_rngC.Next() - 0.5f) * 16f, Y = e.Y + (_rngC.Next() - 0.5f) * 16f });
            float r = _rngLoot.Next();
            if (r < 0.03f) Pickups.Add(new BomberPickup { Kind = "repair", X = e.X, Y = e.Y }); else if (r < 0.05f) Pickups.Add(new BomberPickup { Kind = "emp", X = e.X, Y = e.Y });
        }

        void Explode(float x, float y, float r, float dmg, bool hurtsPlayer, bool fromPlayer, float slow = 0f)
        {
            if (fromPlayer && Is("sapper")) r *= 1.25f;
            Shake?.Invoke(0.7f);
            Effects.Add(new BomberEffect { Kind = "blast", X = x, Y = y, R = r, T = 0.35f, Max = 0.35f });
            Effects.Add(new BomberEffect { Kind = "ring", X = x, Y = y, R = r * 1.3f, T = 0.45f, Max = 0.45f, Color = new Color32(255, 200, 120, 255) });
            NewDecals.Add((x, y, r * 0.8f, new Color32(0, 0, 0, 255)));
            for (int k = 0; k < 26; k++) Spark(x, y, k % 2 == 1 ? new Color32(255, 179, 71, 255) : new Color32(255, 90, 110, 255), 320f);
            if (hurtsPlayer && Dist(x, y, P.X, P.Y) < r + P.R) Hurt();
            foreach (var e in Enemies.ToArray()) if (Dist(x, y, e.X, e.Y) < r + e.R) DamageEnemy(e, dmg, Mathf.Atan2(e.Y - y, e.X - x), 200f, false, false, slow);
            foreach (var b in Barrels) if (b.Hp > 0f && Dist(x, y, b.X, b.Y) < r) { b.Hp = 0f; _barrelQueue.Add((b, 0.12f)); }
            if (fromPlayer) foreach (var s in Shots) if (s.Enemy && Dist(s.X, s.Y, x, y) < r) s.Dead = true;
        }
    }
}
