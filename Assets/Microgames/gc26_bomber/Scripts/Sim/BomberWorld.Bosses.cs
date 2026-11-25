using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // Sector bosses, floor hazards, mines and delayed events — ported from the prototype (mkBoss, bossUpdate, bossChoose, bossDefeated).
    public sealed partial class BomberWorld
    {
        public readonly List<BomberZone> Zones = new List<BomberZone>();
        /// <summary>Boss index (BomberData.Bosses) for each sector of this run.</summary>
        public readonly int[] BossOrder = new int[3];
        public int Bosses { get; private set; }
        public BomberEnemy CurrentBoss { get { foreach (var e in Enemies) if (e.IsBoss) return e; return null; } }

        readonly List<(float t, Action a)> _later = new List<(float, Action)>();

        void Later(float t, Action a) => _later.Add((t, a));

        void TickLater(float dt)
        {
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                var (t, a) = _later[i]; t -= dt;
                if (t > 0f) { _later[i] = (t, a); continue; }
                _later.RemoveAt(i); a();
            }
        }

        void PickBosses()
        {
            var rb = new BomberRng(BomberRng.Hash(Seed, 77u));
            BossOrder[0] = rb.Next() < 0.5f ? 0 : 1; BossOrder[1] = rb.Next() < 0.5f ? 2 : 3; BossOrder[2] = 4;
        }

        // ---- hooks for EditMode tests ----
        public void EnterSectorForTests(int s) => EnterSector(s);
        public void EnterBossForTests(int bi) { BossOrder[Sector] = bi; EnterRoom("boss"); }
        public void SpawnForTests(string kind, float x, float y, bool elite) => Enemies.Add(MakeEnemy(kind, x, y, elite));

        BomberEnemy MakeBoss(int bi)
        {
            var d = BomberData.Bosses[bi]; float hp = d.Hp * BomberData.BossHp;
            return new BomberEnemy { Kind = "boss", Boss = d, Def = d.Def, X = W / 2f, Y = H * 0.28f, R = d.R, Hp = hp, MaxHp = hp, Tm = 1.4f, Face = Mathf.PI, SpdMul = 1f };
        }

        // ================= hazards =================
        void AddHotZones(List<(int x, int y)> free, int n)
        {
            for (int i = 0; i < n && free.Count > 0; i++)
            {
                var c = free[_rngL.Range(free.Count)];
                Zones.Add(new BomberZone { Kind = "hot", X = c.x * Cell - 16f, Y = c.y * Cell - 16f, W = Cell * 3f, H = Cell * 2f, Life = 1e9f });
            }
        }

        void UpdateZones(float dt)
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                var z = Zones[i];
                if (z.Kind == "hot") { if (z.Contains(P.X, P.Y) && Core.CanFire && Core.Heat + 15f * dt < 99.9f) Core.AddHeat(15f * dt); continue; }
                if (z.Tele > 0f) { z.Tele -= dt; continue; }
                z.Life -= dt; if (z.Life <= 0f) { Zones.RemoveAt(i); continue; }
                if (z.Contains(P.X, P.Y) && P.DashT <= 0f) Hurt();
            }
        }

        /// <summary>Mine / mortar shell timer ran out.</summary>
        void Detonate(BomberEffect e)
        {
            float r = e.R > 0f ? e.R : 52f;
            Effects.Add(new BomberEffect { Kind = "blast", X = e.X, Y = e.Y, R = r, T = 0.3f, Max = 0.3f });
            var col = e.Kind == "shell" ? new Color32(255, 169, 77, 255) : new Color32(109, 200, 255, 255);
            for (int k = 0; k < 12; k++) Spark(e.X, e.Y, col, 220f);
            Shake?.Invoke(0.4f);
            if (Dist(P.X, P.Y, e.X, e.Y) < r + P.R) Hurt();
        }

        void Summon(string kind, int n)
        {
            if (Enemies.Count > 10) return;
            var fc = FreeCells();
            for (int i = 0; i < n && fc.Count > 0; i++)
            {
                var c = fc[_rngC.Range(fc.Count)];
                Effects.Add(new BomberEffect { Kind = "spawn", X = c.x * Cell + 16f, Y = c.y * Cell + 16f, T = 0.7f, Max = 0.7f, Spawn = kind });
            }
        }

        // ================= boss AI =================
        float IdleTime(BomberEnemy b) => b.Phase < b.Boss.Idle.Length ? b.Boss.Idle[b.Phase] : 0.5f;

        void UpdateBoss(BomberEnemy b, float dt)
        {
            var d = b.Boss; float frac = b.Hp / b.MaxHp;
            while (b.Phase < d.Phases.Length && frac < d.Phases[b.Phase])
            {
                b.Phase++; Toast?.Invoke($"{d.Name}: фаза {b.Phase + 1}", 1.8f); Shake?.Invoke(1f); b.Act = "idle"; b.Tm = 0.9f;
                AddRing(b.X, b.Y, b.R + 40f, d.Color, 0.5f);
            }
            if (b.Alpha > 0.5f && Dist(b, P) < b.R + P.R) Hurt();
            b.Tm -= dt * (b.Slow > 0f ? 0.7f : 1f);
            float a = Ang(b, P);
            if ((d.Key == "loader" && b.Phase >= 1) || (d.Key == "oracle" && b.Phase >= 2) || (d.Key == "warden" && b.Phase >= 1))
            {
                b.SummonT -= dt;
                if (b.SummonT <= 0f) { b.SummonT = 7f; Summon(d.Key == "loader" ? "beetle" : d.Key == "warden" ? "laser" : "phantom", d.Key == "warden" ? 1 : 2); }
            }
            switch (b.Act)
            {
                case "idle":
                {
                    float tx = P.X, ty = P.Y, sp = 75f;
                    if (d.Key == "smelter") { tx = W / 2f + Mathf.Cos(Time * 0.9f) * 90f; ty = H * 0.38f + Mathf.Sin(Time * 0.7f) * 80f; sp = 30f; }
                    if (d.Key == "warden") { tx = W / 2f; ty = H / 2f; sp = 60f; }
                    if (d.Key == "hive") sp = 30f; if (d.Key == "oracle") sp = 45f;
                    float aa = Mathf.Atan2(ty - b.Y, tx - b.X);
                    if (Dist(tx, ty, b.X, b.Y) > 90f) { b.Vx = Mathf.Cos(aa) * sp; b.Vy = Mathf.Sin(aa) * sp; } else { b.Vx *= 0.9f; b.Vy *= 0.9f; }
                    MoveBody(b, b.Vx * dt, b.Vy * dt);
                    if (b.Tm <= 0f) BossChoose(b);
                    break;
                }
                case "chargeTele": if (b.Tm > 0.3f) b.AimA = a; if (b.Tm <= 0f) { b.Act = "charge"; b.Tm = 1.3f; } break;
                case "charge":
                {
                    float ox = b.X, oy = b.Y, sp = 680f * dt; MoveBody(b, Mathf.Cos(b.AimA) * sp, Mathf.Sin(b.AimA) * sp);
                    if (Dist(b.X, b.Y, ox, oy) < sp * 0.5f || b.Tm <= 0f)
                    {
                        Shake?.Invoke(0.9f); for (int k = 0; k < 20; k++) Spark(b.X + Mathf.Cos(b.AimA) * b.R, b.Y + Mathf.Sin(b.AimA) * b.R, new Color32(255, 179, 71, 255), 260f);
                        if (b.Phase >= 1) for (int i = 0; i < 12; i++) EnemyShot(b.X, b.Y, i * Mathf.PI / 6f, 180f, Orange);
                        b.Act = "recover"; b.Tm = 0.75f;
                    }
                    break;
                }
                case "recover": if (b.Tm <= 0f) { b.Act = "idle"; b.Tm = IdleTime(b); } break;
                case "throwTele":
                    if (b.Tm <= 0f)
                    {
                        foreach (var t in b.Targets)
                        {
                            Effects.Add(new BomberEffect { Kind = "blast", X = t.x, Y = t.y, R = 46f, T = 0.3f, Max = 0.3f });
                            for (int k = 0; k < 10; k++) Spark(t.x, t.y, new Color32(255, 179, 71, 255), 200f);
                            if (Dist(P.X, P.Y, t.x, t.y) < 46f + P.R) Hurt();
                        }
                        Shake?.Invoke(0.6f); b.Targets.Clear(); b.Act = "idle"; b.Tm = IdleTime(b);
                    }
                    break;
                case "ring":
                    if (b.Tm <= 0f)
                    {
                        const int n = 22; float off = b.Cnt * 0.17f + _rngC.Next();
                        for (int i = 0; i < n; i++) EnemyShot(b.X, b.Y, off + i * Mathf.PI * 2f / n, 170f, i % 2 == 1 ? (Color32?)null : Orange);
                        b.Cnt--; b.Tm = 0.34f; if (b.Cnt <= 0) { b.Act = "idle"; b.Tm = IdleTime(b); }
                    }
                    break;
                case "aimed":
                    if (b.Tm <= 0f)
                    {
                        int n = b.Fan > 0 ? b.Fan : 5; float sp = b.Fan > 0 ? 0.15f : 0.16f;
                        for (int i = 0; i < n; i++) EnemyShot(b.X, b.Y, a + (i - (n - 1) / 2f) * sp, 240f, d.Key == "hive" ? new Color32(182, 255, 92, 255) : (Color32?)null);
                        b.Cnt--; b.Tm = 0.33f; if (b.Cnt <= 0) { b.Act = "idle"; b.Tm = IdleTime(b); b.Fan = 0; }
                    }
                    break;
                case "spiral":
                    if (b.Tm <= 0f) { b.Act = "idle"; b.Tm = IdleTime(b); break; }
                    b.AimA += dt * 2.4f; b.Emit -= dt;
                    if (b.Emit <= 0f) { b.Emit = 0.07f; int arms = b.Phase >= 1 ? 3 : 2; for (int k = 0; k < arms; k++) EnemyShot(b.X, b.Y, b.AimA + k * Mathf.PI * 2f / arms, 170f, Orange); }
                    break;
                case "snipe":
                    if (b.Tm > 0.3f) b.AimA = a;
                    if (b.Tm <= 0f)
                    {
                        foreach (var o in new[] { -0.12f, 0f, 0.12f }) EnemyShot(b.X, b.Y, b.AimA + o, 900f, null, 4f);
                        b.Cnt--; if (b.Cnt > 0) b.Tm = 0.75f; else { b.Act = "idle"; b.Tm = IdleTime(b); }
                    }
                    break;
                case "teleOut":
                    b.Alpha = Mathf.Max(0f, b.Tm / 0.5f);
                    if (b.Tm <= 0f)
                    {
                        var fc = FreeCells().FindAll(c => Dist(c.x * Cell, c.y * Cell, P.X, P.Y) > 260f);
                        if (fc.Count > 0) { var c = fc[_rngC.Range(fc.Count)]; b.X = c.x * Cell + 16f; b.Y = c.y * Cell + 16f; PushOut(b); }
                        b.Act = "teleIn"; b.Tm = 0.5f;
                    }
                    break;
                case "teleIn": b.Alpha = 1f - Mathf.Max(0f, b.Tm / 0.5f); if (b.Tm <= 0f) { b.Alpha = 1f; b.Act = "idle"; b.Tm = IdleTime(b) * 0.6f; } break;
                case "sweepTele": if (b.Tm <= 0f) { b.Act = "sweep"; b.Tm = 2.6f; } break;
                case "sweep":
                    b.AimA += dt * 1.15f * b.Sdir;
                    for (int k = 0; k < b.Beams; k++)
                    {
                        float aa = b.AimA + k * Mathf.PI * 2f / b.Beams; var end = RayEnd(b.X, b.Y, aa, 1100f);
                        if (SegDist(P.X, P.Y, b.X, b.Y, end.x, end.y) < P.R + 5f && P.DashT <= 0f) Hurt();
                    }
                    if (b.Tm <= 0f) { b.Act = "idle"; b.Tm = IdleTime(b); }
                    break;
            }
        }

        void BossChoose(BomberEnemy b)
        {
            string key = b.Boss.Key; string[] opts;
            if (key == "loader") opts = b.Phase > 0 ? new[] { "charge", "throw", "charge" } : new[] { "charge", "throw" };
            else if (key == "hive") opts = b.Phase > 0 ? new[] { "brood", "acid", "spray", "ring" } : new[] { "brood", "acid", "spray" };
            else if (key == "smelter") opts = b.Phase > 0 ? new[] { "ring", "lava", "aimed", "spiral" } : new[] { "ring", "aimed", "spiral" };
            else if (key == "warden") opts = b.Phase > 0 ? new[] { "sweep", "mines", "ring", "aimed" } : new[] { "sweep", "mines", "aimed" };
            else
            {
                var l = new List<string> { "spiral", "snipe" };
                if (b.Phase >= 1) l.AddRange(new[] { "charge", "throw" });
                if (b.Phase >= 2) l.AddRange(new[] { "lava", "sweep" });
                opts = l.ToArray();
            }
            var act = opts[(b.Seq++ + (_rngC.Next() < 0.3f ? 1 : 0)) % opts.Length];
            if (key == "oracle" && _rngC.Next() < 0.35f && act != "charge") { b.Act = "teleOut"; b.Tm = 0.5f; return; }
            float a = Ang(b, P);
            switch (act)
            {
                case "charge": b.Act = "chargeTele"; b.Tm = 0.7f; b.AimA = a; break;
                case "throw":
                {
                    int n = b.Phase > 0 || key == "oracle" ? 5 : 3; b.Targets.Clear();
                    for (int i = 0; i < n; i++)
                        b.Targets.Add(new Vector2(Mathf.Clamp(P.X + (i > 0 ? (_rngC.Next() - 0.5f) * 180f : P.Vx * 0.3f), 48f, W - 48f), Mathf.Clamp(P.Y + (i > 0 ? (_rngC.Next() - 0.5f) * 180f : P.Vy * 0.3f), 48f, H - 48f)));
                    b.Act = "throwTele"; b.Tm = 0.85f; break;
                }
                case "ring": b.Act = "ring"; b.Cnt = b.Phase > 0 ? 4 : 3; b.Tm = 0.5f; break;
                case "aimed": b.Act = "aimed"; b.Cnt = 3; b.Tm = 0.4f; break;
                case "spray": b.Act = "aimed"; b.Cnt = 2; b.Tm = 0.45f; b.Fan = 7; break;
                case "spiral": b.Act = "spiral"; b.Tm = 2.6f; b.AimA = a; break;
                case "snipe": b.Act = "snipe"; b.Cnt = 3; b.Tm = 0.9f; b.AimA = a; break;
                case "brood": Summon(_rngC.Next() < 0.5f ? "kamikaze" : "beetle", b.Phase > 0 ? 4 : 3); b.Act = "idle"; b.Tm = IdleTime(b) + 0.6f; break;
                case "acid": case "lava":
                {
                    bool acid = act == "acid"; int n = acid ? 4 : 3;
                    for (int i = 0; i < n; i++)
                    {
                        float w0 = Cell * (acid ? 2 : 3 + _rngC.Range(2)), h0 = Cell * (acid ? 2 : 2 + _rngC.Range(2));
                        float x = i == 0 ? Mathf.Clamp(P.X - w0 / 2f, Cell, W - Cell - w0) : Mathf.Clamp(P.X - w0 / 2f + (_rngC.Next() - 0.5f) * 360f, Cell, W - Cell - w0);
                        float y = i == 0 ? Mathf.Clamp(P.Y - h0 / 2f, Cell, H - Cell - h0) : Mathf.Clamp(P.Y - h0 / 2f + (_rngC.Next() - 0.5f) * 300f, Cell, H - Cell - h0);
                        Zones.Add(new BomberZone { Kind = act, X = x, Y = y, W = w0, H = h0, Tele = acid ? 0.9f : 1.2f, Life = acid ? 4f : 5f });
                    }
                    b.Act = "idle"; b.Tm = IdleTime(b) + 0.6f; break;
                }
                case "sweep":
                    b.Act = "sweepTele"; b.Tm = 1f; b.AimA = a + (_rngC.Next() < 0.5f ? -1f : 1f) * 0.9f; b.Sdir = _rngC.Next() < 0.5f ? 1f : -1f;
                    b.Beams = b.Phase >= 1 ? (key == "warden" ? 3 : 2) : (key == "oracle" ? 2 : 1); break;
                case "mines":
                {
                    var fc = FreeCells(); int n = b.Phase > 0 ? 7 : 5;
                    Effects.Add(new BomberEffect { Kind = "mine", X = P.X, Y = P.Y, T = 1.5f, Max = 1.5f, R = 52f });
                    for (int i = 1; i < n && fc.Count > 0; i++) { var c = fc[_rngC.Range(fc.Count)]; Effects.Add(new BomberEffect { Kind = "mine", X = c.x * Cell + 16f, Y = c.y * Cell + 16f, T = 1.5f + i * 0.08f, Max = 1.5f + i * 0.08f, R = 52f }); }
                    b.Act = "idle"; b.Tm = IdleTime(b) + 0.4f; break;
                }
            }
        }

        void BossDefeated(BomberEnemy b)
        {
            Score += 1000; Bosses++; Shake?.Invoke(1.4f);
            for (int k = 0; k < 60; k++) Spark(b.X, b.Y, k % 3 != 0 ? new Color32(255, 214, 92, 255) : new Color32(255, 255, 255, 255), 420f);
            AddRing(b.X, b.Y, 140f, b.Boss.Color, 0.6f);
            foreach (var s in Shots) if (s.Enemy) s.Dead = true;
            Enemies.Clear();
            Effects.RemoveAll(e => e.Kind == "spawn" || e.Kind == "mine" || e.Kind == "shell");
            Zones.RemoveAll(z => z.Kind == "lava" || z.Kind == "acid");
            RoomCleared();
            if (Sector >= 2) { Later(1.6f, () => Phase = BomberPhase.Victory); return; }
            const int heal = 2; P.Hp = Mathf.Min(P.MaxHp, P.Hp + heal);
            var name = b.Boss.Name;
            Later(0.7f, () => OfferReward($"{name} уничтожен", $"Починено {heal} ячейки. Выбери модуль или компаньона.", "module", "companion"));
        }
    }
}
