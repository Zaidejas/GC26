using UnityEngine;

namespace GC26.Bomber
{
    // Enemies, projectiles, damage and explosions — ported from the prototype (updateEnemy, updateBullets, killEnemy, explode).
    public sealed partial class BomberWorld
    {
        BomberEnemy MakeEnemy(string kind, float x, float y, bool elite)
        {
            var d = BomberData.Enemies[kind]; float hp = d.Hp * HpMul * (elite ? 1.5f : 1f);
            return new BomberEnemy
            {
                Kind = kind, Def = d, X = x, Y = y, R = d.R + (elite ? 2f : 0f), Hp = hp, MaxHp = hp, Elite = elite, Tm = 0.4f + _rngC.Next() * 0.8f,
                Orbit = _rngC.Next() < 0.5f ? 1f : -1f, ShootT = 2f + _rngC.Next() * 1.5f, SpdMul = BomberData.EnemySpeed, Wave = Room != null ? Room.Wave : 0,
            };
        }

        void EnemyShot(float x, float y, float a, float speed, Color32? color = null, float r = 5f)
        {
            float s = speed * BomberData.BulletMul;
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

        Vector2 Toward(BomberEnemy e) => e.Los ? new Vector2(Mathf.Cos(Ang(e, P)), Mathf.Sin(Ang(e, P))) : FlowDir(e);

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
            e.LosT -= dt; if (e.LosT <= 0f) { e.LosT = 0.2f; e.Los = Los(e.X, e.Y, P.X, P.Y); }
            if (e.Stun > 0f) { e.Stun -= dt; e.Vx *= 0.8f; e.Vy *= 0.8f; MoveBody(e, e.Vx * dt, e.Vy * dt); return; }
            float d = Dist(e, P), a = Ang(e, P), spd = e.Def.Speed;
            float el = (e.Elite ? 0.75f : 1f) * BomberData.Interval * (e.Slow > 0f ? 1.5f : 1f);
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
            }
        }

        public void DamageEnemy(BomberEnemy e, float amt, float a = 0f, float kb = 0f)
        {
            if (e.Hp <= 0f) return;
            e.Hp -= amt; e.Flash = 0.08f;
            if (kb > 0f && e.Kind != "turret") { e.Vx += Mathf.Cos(a) * kb; e.Vy += Mathf.Sin(a) * kb; }
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
            Kills++; Combo++; ComboT = 2.6f; MaxCombo = Mathf.Max(MaxCombo, Combo);
            if (Combo % 5 == 0)
            {
                for (int k = 0; k < 3; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = e.X + (_rngC.Next() - 0.5f) * 20f, Y = e.Y + (_rngC.Next() - 0.5f) * 20f });
                Texts.Add(new BomberText { X = e.X, Y = e.Y - 24, S = $"КОМБО ×{Combo}", T = 0.9f, Color = new Color32(255, 214, 92, 255) });
            }
            if (Combo == 10) Toast?.Invoke("В УДАРЕ: +10% скорости, пока держишь комбо", 1.8f);
            Score += Mathf.RoundToInt((e.Elite ? 50 : 10) * (1f + Mathf.Min(Combo, 20) * 0.05f));
            if (e.Kind == "splitter") foreach (var o in new[] { -1f, 1f }) { var m = MakeEnemy("mini", e.X + o * 10f, e.Y, false); m.Wave = e.Wave; m.Tm = 0.4f; Enemies.Add(m); }
            if (e.Kind == "mini") return;
            int n = 1 + _rngLoot.Range(3) + (e.Elite ? 4 : 0);
            for (int k = 0; k < n; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = e.X + (_rngC.Next() - 0.5f) * 16f, Y = e.Y + (_rngC.Next() - 0.5f) * 16f });
            float r = _rngLoot.Next();
            if (r < 0.03f) Pickups.Add(new BomberPickup { Kind = "repair", X = e.X, Y = e.Y }); else if (r < 0.05f) Pickups.Add(new BomberPickup { Kind = "emp", X = e.X, Y = e.Y });
        }

        void Explode(float x, float y, float r, float dmg, bool hurtsPlayer, bool fromPlayer)
        {
            Shake?.Invoke(0.7f);
            Effects.Add(new BomberEffect { Kind = "blast", X = x, Y = y, R = r, T = 0.35f, Max = 0.35f });
            Effects.Add(new BomberEffect { Kind = "ring", X = x, Y = y, R = r * 1.3f, T = 0.45f, Max = 0.45f, Color = new Color32(255, 200, 120, 255) });
            NewDecals.Add((x, y, r * 0.8f, new Color32(0, 0, 0, 255)));
            for (int k = 0; k < 26; k++) Spark(x, y, k % 2 == 1 ? new Color32(255, 179, 71, 255) : new Color32(255, 90, 110, 255), 320f);
            if (hurtsPlayer && Dist(x, y, P.X, P.Y) < r + P.R) Hurt();
            foreach (var e in Enemies.ToArray()) if (Dist(x, y, e.X, e.Y) < r + e.R) DamageEnemy(e, dmg, Mathf.Atan2(e.Y - y, e.X - x), 200f);
            foreach (var b in Barrels) if (b.Hp > 0f && Dist(x, y, b.X, b.Y) < r) { b.Hp = 0f; _barrelQueue.Add((b, 0.12f)); }
            if (fromPlayer) foreach (var s in Shots) if (s.Enemy && Dist(s.X, s.Y, x, y) < r) s.Dead = true;
        }

        void UpdateShots(float dt)
        {
            foreach (var b in Shots)
            {
                if (b.Dead) continue;
                b.Life -= dt; if (b.Life <= 0f) { b.Dead = true; continue; }
                float sp = Mathf.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy); int n = Mathf.Max(1, Mathf.CeilToInt(sp * dt / 8f));
                for (int s = 0; s < n && !b.Dead; s++)
                {
                    b.X += b.Vx * dt / n; b.Y += b.Vy * dt / n;
                    if (SolidAt(b.X, b.Y, b.R * 0.6f) || b.X < 0 || b.Y < 0 || b.X > W || b.Y > H)
                    {
                        for (int k = 0; k < 4; k++) Spark(b.X, b.Y, b.Color, 120f);
                        b.Dead = true; break;
                    }
                    if (!b.Enemy)
                    {
                        foreach (var br in Barrels) if (br.Hp > 0f && Dist(br.X, br.Y, b.X, b.Y) < br.R + b.R) { br.Hp = 0f; _barrelQueue.Add((br, 0.12f)); b.Dead = true; break; }
                        if (b.Dead) break;
                        foreach (var e in Enemies)
                        {
                            if (e.Hp <= 0f || e.Alpha < 0.5f) continue;
                            if (Dist(e.X, e.Y, b.X, b.Y) < e.R + b.R) { DamageEnemy(e, b.Dmg, Mathf.Atan2(b.Vy, b.Vx), b.Heavy ? 160f : 60f); b.Dead = true; break; }
                        }
                    }
                    else if (Dist(P.X, P.Y, b.X, b.Y) < P.R * 0.75f + b.R && P.ITime <= 0f) { Hurt(); b.Dead = true; }
                }
            }
            Shots.RemoveAll(b => b.Dead);
        }
    }
}
