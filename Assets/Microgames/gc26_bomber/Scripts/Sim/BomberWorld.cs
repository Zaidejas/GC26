using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public enum BomberPhase { Play, Dead, SectorClear }

    /// <summary>What the phone controls ask for this frame.</summary>
    public struct BomberIntent
    {
        public Vector2 Move;          // analog, length <= 1, y DOWN (screen space)
        public bool Dash, Vent, Emp, Ability, ForsageToggle, AltPressed, AltHeld;
    }

    /// <summary>
    /// The whole run simulation, ported from the HTML prototype (portrait phone rooms 17x30 cells).
    /// Plain C# object owned by BomberContext; the view and the UI only read it.
    /// </summary>
    public sealed partial class BomberWorld
    {
        const float Cell = BomberData.Cell, W = BomberData.W, H = BomberData.H;
        const int GW = BomberData.GW, GH = BomberData.GH;

        // ---- public state ----
        public BomberPhase Phase { get; private set; } = BomberPhase.Play;
        public readonly BomberPlayerState P = new BomberPlayerState();
        public readonly HeatModel Core = new HeatModel(HeatSettings.Default);
        public readonly HeatGovernor Governor = new HeatGovernor();
        public readonly List<BomberEnemy> Enemies = new List<BomberEnemy>();
        public readonly List<BomberShot> Shots = new List<BomberShot>();
        public readonly List<BomberPickup> Pickups = new List<BomberPickup>();
        public readonly List<BomberEffect> Effects = new List<BomberEffect>();
        public readonly List<BomberParticle> Particles = new List<BomberParticle>();
        public readonly List<BomberText> Texts = new List<BomberText>();
        public readonly List<BomberWall> Walls = new List<BomberWall>();
        public readonly List<BomberDoor> Doors = new List<BomberDoor>();
        public readonly List<BomberBarrel> Barrels = new List<BomberBarrel>();
        /// <summary>Scorch marks and oil left by deaths and explosions this room; the view drains it into the floor.</summary>
        public readonly List<(float x, float y, float r, Color32 c)> NewDecals = new List<(float, float, float, Color32)>();
        public BomberRoomState Room { get; private set; }
        public readonly List<List<string>>[] Plan = new List<List<string>>[3];
        public uint Seed { get; private set; }
        public int Sector { get; private set; }
        public int Step { get; private set; }
        public int Score, Kills, Rooms, Combo, MaxCombo, DamageTaken;
        public float ComboT, Time, Flash;
        public bool RoomDamaged;
        public int RoomVersion { get; private set; } // bumps when a new room is built (the view rebuilds walls)

        // ---- events for view / audio ----
        public event Action<float> Shake;
        public event Action<string, float> Toast;

        // ---- internals ----
        BomberRng _rngL, _rngE, _rngLoot, _rngC;
        readonly byte[] _blocked = new byte[GW * GH];
        readonly short[] _flow = new short[GW * GH];
        readonly int[] _flowQ = new int[GW * GH];
        float _flowTimer;
        readonly List<(BomberBarrel b, float t)> _barrelQueue = new List<(BomberBarrel, float)>();

        // ================= run setup =================
        public void StartRun(uint seed)
        {
            Seed = seed == 0 ? 1u : seed;
            Phase = BomberPhase.Play;
            Score = Kills = Rooms = Combo = MaxCombo = DamageTaken = 0; ComboT = Time = 0f;
            P.R = 9f; P.MaxHp = P.Hp = 6; P.Emp = 1; P.Scrap = 0; P.ITime = 0f; P.AbCd = P.AbT = 0f; P.TacCd = 0f;
            Core.Vent(HeatModel.Max); Governor.Forsage = false;
            for (int s = 0; s < 3; s++) Plan[s] = MakePlan(new BomberRng(BomberRng.Hash(Seed, (uint)(100 + s))));
            EnterSector(0);
        }

        static List<List<string>> MakePlan(BomberRng r)
        {
            var plan = new List<List<string>>
            {
                new List<string> { "start" },
                r.Next() < 0.5f ? new List<string> { "combat", "elite" } : new List<string> { "combat" },
                Shuffled(r, r.Next() < 0.5f ? "chest" : "altar", "combat"),
                new List<string> { "shop" },
                Shuffled(r, "combat", "elite"),
                Shuffled(r, "workbench", "combat"),
                new List<string> { "boss" },
            };
            return plan;
        }
        static List<string> Shuffled(BomberRng r, params string[] items) { var l = new List<string>(items); r.Shuffle(l); return l; }

        void EnterSector(int s)
        {
            Sector = s; Step = 0;
            _rngL = new BomberRng(BomberRng.Hash(Seed, (uint)(1 + s * 7)));
            _rngE = new BomberRng(BomberRng.Hash(Seed, (uint)(2 + s * 7)));
            _rngLoot = new BomberRng(BomberRng.Hash(Seed, (uint)(3 + s * 7)));
            _rngC = new BomberRng(BomberRng.Hash(Seed, (uint)(4 + s * 7)));
            EnterRoom("start");
            Toast?.Invoke($"Сектор {s + 1}: {BomberData.Sectors[s].Name}. {BomberData.Sectors[s].Line}", 3.5f);
        }

        List<string> NextOptions() => Step + 1 < Plan[Sector].Count ? Plan[Sector][Step + 1] : new List<string>();

        void EnterRoom(string type)
        {
            Room = new BomberRoomState { Type = type };
            Enemies.Clear(); Shots.Clear(); Pickups.Clear(); Effects.Clear(); Particles.Clear(); Texts.Clear(); Walls.Clear(); Doors.Clear(); Barrels.Clear(); _barrelQueue.Clear(); NewDecals.Clear();
            Room.Exits = NextOptions();
            var gaps = BuildBorder(Room.Exits);
            bool calm = type == "shop" || type == "chest" || type == "workbench" || type == "altar";
            if (type == "boss") foreach (var (x, y) in new[] { (7, 4), (7, 11), (21, 4), (21, 11) }) Walls.Add(Rect(y, x, 2, 2, cover: true)); // transposed landscape layout
            else if (calm) foreach (var (x, y) in new[] { (5, 3), (23, 3), (5, 13), (23, 13) }) Walls.Add(Rect(y, x, 1, 2, cover: true));
            else PlaceCovers(gaps);
            ComputeBlocked();
            P.X = W / 2f; P.Y = H - 240f; // above the touch controls P.Vx = P.Vy = 0f; P.DashT = 0f;
            var free = FreeCells();
            if (type == "combat" || type == "elite" || type == "start")
            {
                if (Sector == 0) { int n = 2 + _rngL.Range(2); for (int i = 0; i < n && free.Count > 0; i++) { int k = _rngL.Range(free.Count); var c = free[k]; free.RemoveAt(k); Barrels.Add(new BomberBarrel { X = c.x * Cell + 16, Y = c.y * Cell + 16 }); } }
                Room.Waves = MakeWaves(type); Room.Locked = true;
            }
            else if (type == "boss")
            {
                // Bosses are the next porting step: for now reaching the boss room completes the sector.
                Phase = BomberPhase.SectorClear;
            }
            else
            {
                OpenDoors();
                Toast?.Invoke($"{BomberData.RoomLabel[type]}: эта комната появится на следующем этапе переноса", 2.4f);
            }
            ComputeFlow();
            RoomVersion++;
        }

        // ================= room geometry =================
        static BomberWall Rect(int cx, int cy, int cw, int ch, bool cover = false, bool doorOut = false, bool doorIn = false)
            => new BomberWall { X = cx * Cell, Y = cy * Cell, W = cw * Cell, H = ch * Cell, Cover = cover, DoorOut = doorOut, DoorIn = doorIn };

        /// <summary>Portrait room: entrance at the bottom (x 7..9), exits in the top wall.</summary>
        List<(int a, int b)> BuildBorder(List<string> exits)
        {
            Walls.Add(Rect(0, 0, 1, GH)); Walls.Add(Rect(GW - 1, 0, 1, GH));
            Walls.Add(Rect(1, GH - 1, 6, 1)); Walls.Add(Rect(10, GH - 1, 6, 1)); Walls.Add(Rect(7, GH - 1, 3, 1, doorIn: true));
            var gaps = exits.Count == 2 ? new List<(int, int)> { (3, 5), (11, 13) } : exits.Count == 1 ? new List<(int, int)> { (7, 9) } : new List<(int, int)>();
            int x = 1;
            foreach (var g in gaps) { if (g.Item1 > x) Walls.Add(Rect(x, 0, g.Item1 - x, 1)); x = g.Item2 + 1; }
            if (x < GW - 1) Walls.Add(Rect(x, 0, GW - 1 - x, 1));
            for (int i = 0; i < gaps.Count; i++)
            {
                var b = Rect(gaps[i].Item1, 0, gaps[i].Item2 - gaps[i].Item1 + 1, 1, doorOut: true);
                Walls.Add(b);
                Doors.Add(new BomberDoor { Type = exits[i], X0 = gaps[i].Item1 * Cell, X1 = (gaps[i].Item2 + 1) * Cell, Blocker = b });
            }
            return gaps;
        }

        void PlaceCovers(List<(int a, int b)> gaps)
        {
            var shapes = new[] { (2, 2), (3, 1), (1, 3), (2, 1), (1, 2), (4, 1) };
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var added = new List<(int x, int y, int w, int h)>();
                int n = 3 + _rngL.Range(4);
                for (int i = 0; i < n; i++)
                {
                    var (w, h) = _rngL.Pick(shapes);
                    int x = 2 + (int)(_rngL.Next() * (GW - 4 - w)), y = 5 + (int)(_rngL.Next() * (GH - 11 - h));
                    bool ok = true; foreach (var a in added) if (!(x + w + 1 <= a.x || a.x + a.w + 1 <= x || y + h + 1 <= a.y || a.y + a.h + 1 <= y)) { ok = false; break; }
                    if (ok) added.Add((x, y, w, h));
                }
                var rs = new List<BomberWall>(); foreach (var a in added) rs.Add(Rect(a.x, a.y, a.w, a.h, cover: true));
                Walls.AddRange(rs); ComputeBlocked();
                if (Reachable(gaps)) return;
                foreach (var r in rs) Walls.Remove(r);
            }
            ComputeBlocked();
        }

        bool Reachable(List<(int a, int b)> gaps)
        {
            var seen = new bool[GW * GH]; var tmp = (byte[])_blocked.Clone();
            foreach (var g in gaps) for (int k = g.a; k <= g.b; k++) tmp[k] = 0;
            var q = new Stack<int>(); int start = (GH - 3) * GW + 8; q.Push(start); seen[start] = true;
            while (q.Count > 0)
            {
                int c = q.Pop(), x = c % GW, y = c / GW;
                Try(x + 1, y); Try(x - 1, y); Try(x, y + 1); Try(x, y - 1);
                void Try(int nx, int ny) { if (nx < 0 || ny < 0 || nx >= GW || ny >= GH) return; int n = ny * GW + nx; if (seen[n] || tmp[n] != 0) return; seen[n] = true; q.Push(n); }
            }
            int free = 0, got = 0; for (int i = 0; i < GW * GH; i++) { if (tmp[i] == 0) free++; if (seen[i]) got++; }
            if (got != free) return false;
            foreach (var g in gaps) if (!seen[g.a + 1]) return false;
            return true;
        }

        void ComputeBlocked()
        {
            Array.Clear(_blocked, 0, _blocked.Length);
            foreach (var w in Walls)
            {
                if (!w.On) continue;
                int x0 = Mathf.FloorToInt(w.X / Cell), x1 = Mathf.CeilToInt((w.X + w.W) / Cell) - 1, y0 = Mathf.FloorToInt(w.Y / Cell), y1 = Mathf.CeilToInt((w.Y + w.H) / Cell) - 1;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (x >= 0 && y >= 0 && x < GW && y < GH) _blocked[y * GW + x] = 1;
            }
        }

        void ComputeFlow()
        {
            for (int i = 0; i < _flow.Length; i++) _flow[i] = -1;
            int sx = Mathf.Clamp((int)(P.X / Cell), 0, GW - 1), sy = Mathf.Clamp((int)(P.Y / Cell), 0, GH - 1);
            int h = 0, t = 0, s = sy * GW + sx; _flowQ[t++] = s; _flow[s] = 0;
            while (h < t)
            {
                int c = _flowQ[h++], x = c % GW, y = c / GW;
                if (x > 0) Visit(c - 1); if (x < GW - 1) Visit(c + 1); if (y > 0) Visit(c - GW); if (y < GH - 1) Visit(c + GW);
                void Visit(int n) { if (_blocked[n] == 0 && _flow[n] < 0) { _flow[n] = (short)(_flow[c] + 1); _flowQ[t++] = n; } }
            }
        }

        List<(int x, int y)> FreeCells()
        {
            var o = new List<(int, int)>();
            for (int y = 3; y < GH - 6; y++) for (int x = 2; x < GW - 2; x++)
                if (_blocked[y * GW + x] == 0 && _blocked[y * GW + x + 1] == 0 && _blocked[(y + 1) * GW + x] == 0 && _blocked[(y - 1) * GW + x] == 0) o.Add((x, y));
            return o;
        }

        void OpenDoors()
        {
            foreach (var d in Doors) { d.Open = true; d.Blocker.On = false; }
            ComputeBlocked();
        }

        // ================= physics helpers =================
        public bool SolidAt(float x, float y, float r)
        {
            foreach (var w in Walls)
            {
                if (!w.On) continue;
                float nx = Mathf.Clamp(x, w.X, w.X + w.W), ny = Mathf.Clamp(y, w.Y, w.Y + w.H), dx = x - nx, dy = y - ny;
                if (dx * dx + dy * dy < r * r) return true;
            }
            return false;
        }

        void PushOut(BomberBody e)
        {
            foreach (var w in Walls)
            {
                if (!w.On) continue;
                float nx = Mathf.Clamp(e.X, w.X, w.X + w.W), ny = Mathf.Clamp(e.Y, w.Y, w.Y + w.H), dx = e.X - nx, dy = e.Y - ny, d2 = dx * dx + dy * dy;
                if (d2 >= e.R * e.R) continue;
                if (d2 > 1e-4f) { float d = Mathf.Sqrt(d2); e.X += dx / d * (e.R - d); e.Y += dy / d * (e.R - d); }
                else
                {
                    float l = e.X - w.X, r = w.X + w.W - e.X, t = e.Y - w.Y, b = w.Y + w.H - e.Y, m = Mathf.Min(Mathf.Min(l, r), Mathf.Min(t, b));
                    if (m == l) e.X = w.X - e.R; else if (m == r) e.X = w.X + w.W + e.R; else if (m == t) e.Y = w.Y - e.R; else e.Y = w.Y + w.H + e.R;
                }
            }
        }

        void MoveBody(BomberBody e, float dx, float dy)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / (e.R * 0.7f)));
            for (int i = 0; i < n; i++) { e.X += dx / n; e.Y += dy / n; PushOut(e); }
            e.X = Mathf.Clamp(e.X, e.R, W - e.R); e.Y = Mathf.Clamp(e.Y, e.R, H - e.R);
        }

        public bool Los(float ax, float ay, float bx, float by)
        {
            float d = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); int n = Mathf.CeilToInt(d / 12f);
            for (int i = 1; i < n; i++) if (SolidAt(ax + (bx - ax) * i / n, ay + (by - ay) * i / n, 2f)) return false;
            return true;
        }

        static float Dist(BomberBody a, BomberBody b) => Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        static float Ang(BomberBody a, BomberBody b) => Mathf.Atan2(b.Y - a.Y, b.X - a.X);
        static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        // ================= main loop =================
        public void Tick(float dt, BomberIntent intent)
        {
            if (Phase != BomberPhase.Play) return;
            Time += dt;
            if (Room.Fade > 0f) Room.Fade -= dt;
            if (Flash > 0f) Flash -= dt;
            if (ComboT > 0f) { ComboT -= dt; if (ComboT <= 0f) Combo = 0; }
            Core.Tick(dt);
            UpdatePlayer(dt, intent);
            if (Phase != BomberPhase.Play) return;

            _flowTimer -= dt; if (_flowTimer <= 0f) { _flowTimer = 0.2f; ComputeFlow(); }

            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var e = Effects[i]; e.T -= dt;
                if (e.T > 0f) continue;
                Effects.RemoveAt(i);
                if (e.Kind == "spawn") Enemies.Add(MakeEnemy(e.Spawn, e.X, e.Y, e.Elite));
            }
            foreach (var e in Enemies.ToArray()) UpdateEnemy(e, dt);
            UpdateShots(dt);
            for (int i = _barrelQueue.Count - 1; i >= 0; i--)
            {
                var (b, t) = _barrelQueue[i]; t -= dt;
                if (t > 0f) { _barrelQueue[i] = (b, t); continue; }
                _barrelQueue.RemoveAt(i);
                if (Barrels.Remove(b)) Explode(b.X, b.Y, 80f, 40f, false, false);
            }
            for (int i = Particles.Count - 1; i >= 0; i--) { var p = Particles[i]; p.Life -= dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vx *= 0.9f; p.Vy *= 0.9f; if (p.Life <= 0f) Particles.RemoveAt(i); }
            for (int i = Texts.Count - 1; i >= 0; i--) { var t = Texts[i]; t.T -= dt; t.Y -= 22f * dt; if (t.T <= 0f) Texts.RemoveAt(i); }
            UpdateRoom(dt);
        }

        // ================= player =================
        const float Speed = 192f, DashSpeed = 853f, DashTime = 0.15f, DashCooldown = 0.8f;
        const float ShotSpeed = 720f, ShotDamage = 10f, ShotRate = 4f, ShotHeat = 4f, ShotSpread = 0.02f;
        const float AbilityCooldown = 16f, AbilityTime = 5f; // Spark: "Разгон" — shots do not heat the core

        public string StatusLine
        {
            get
            {
                if (Core.State == CoreState.Overheated) return $"ПЕРЕГРЕВ {Core.OverheatLeft:0.0} с — нет оружия";
                if (P.AbT > 0f) return "РАЗГОН — выстрелы не греют ядро";
                if (Core.State == CoreState.Overdrive) return "ФОРСАЖ — урон +30%";
                if (Governor.Forsage) return Core.Heat >= 88f ? "Почти перегрев! Сбрось или выключи" : "ФОРСАЖ — ядро греется";
                return "Безопасная зона";
            }
        }

        void UpdatePlayer(float dt, BomberIntent it)
        {
            var mv = it.Move; if (mv.sqrMagnitude > 1f) mv.Normalize();
            P.DashCd -= dt; P.ITime -= dt; P.FireCd -= dt; P.MuzzleT -= dt; P.AltCd -= dt; P.TacCd -= dt; P.Bob += dt;
            if (P.AbT > 0f) P.AbT -= dt; else P.AbCd -= dt;

            if (it.Dash && P.DashCd <= 0f && Core.CanFire)
            {
                var dir = mv.sqrMagnitude > 0.01f ? mv.normalized : new Vector2(Mathf.Cos(P.Aim), Mathf.Sin(P.Aim));
                P.Dx = dir.x; P.Dy = dir.y; P.DashT = DashTime; P.ITime = Mathf.Max(P.ITime, 0.2f); P.DashCd = DashCooldown;
            }
            if (P.DashT > 0f) { P.DashT -= dt; P.Vx = P.Dx * DashSpeed; P.Vy = P.Dy * DashSpeed; Particle(P.X, P.Y, 0, 0, 0.25f, new Color32(127, 227, 255, 90), P.R); }
            else
            {
                float sp = Speed * (Core.CanFire ? 1f : 0.55f) * (Combo >= 10 ? 1.1f : 1f), k = Mathf.Min(1f, dt * 18f);
                P.Vx += (mv.x * sp - P.Vx) * k; P.Vy += (mv.y * sp - P.Vy) * k;
                if (mv.sqrMagnitude > 0.01f && UnityEngine.Random.value < 0.55f) Particle(P.X - P.Vx * 0.04f, P.Y - P.Vy * 0.04f, -P.Vx * 0.3f, -P.Vy * 0.3f, 0.22f, new Color32(127, 227, 255, 140), 2.5f);
            }
            if (Core.State == CoreState.Overdrive && UnityEngine.Random.value < 0.3f) Particle(P.X + (UnityEngine.Random.value - 0.5f) * 14f, P.Y + (UnityEngine.Random.value - 0.5f) * 14f, 0f, -50f, 0.3f, new Color32(255, 179, 71, 160), 2f);
            if (Core.State == CoreState.Overheated && UnityEngine.Random.value < 0.4f) Spark(P.X + (UnityEngine.Random.value - 0.5f) * 12f, P.Y + (UnityEngine.Random.value - 0.5f) * 12f, new Color32(255, 154, 92, 255), 60f);
            MoveBody(P, P.Vx * dt, P.Vy * dt);

            if (it.ForsageToggle && Core.CanFire) { Governor.Forsage = !Governor.Forsage; Toast?.Invoke(Governor.Forsage ? "ФОРСАЖ: ядро разгоняется, урон +30%. Следи за перегревом!" : "Ядро в безопасной зоне", 1.4f); }
            if (it.Vent && P.TacCd <= 0f && Core.CanFire) Vent();
            if (it.Emp && P.Emp > 0) Emp();
            if (it.Ability && P.AbCd <= 0f && P.AbT <= 0f) { P.AbT = AbilityTime; P.AbCd = AbilityCooldown; AddRing(P.X, P.Y, 40f, new Color32(127, 227, 255, 255), 0.4f); Toast?.Invoke("Разгон: выстрелы не греют ядро", 1f); }

            // auto aim: nearest enemy, preferring ones in line of sight
            BomberEnemy best = null; float bs = 1e9f;
            foreach (var e in Enemies) { if (e.Alpha < 0.5f || e.Hp <= 0f) continue; float sc = Dist(P, e) + (Los(P.X, P.Y, e.X, e.Y) ? 0f : 600f); if (sc < bs) { bs = sc; best = e; } }
            if (best != null) P.Aim = Ang(P, best);
            else if (mv.sqrMagnitude > 0.01f) P.Aim = Mathf.Atan2(mv.y, mv.x);

            P.Firing = best != null && bs < 600f && Governor.AllowShot(Core, P.AbT > 0f ? 0f : ShotHeat);
            if (P.Firing && Core.CanFire && P.FireCd <= 0f)
            {
                Fire(1f, false); if (P.AbT <= 0f) Core.AddHeat(ShotHeat);
                P.FireCd = 1f / (ShotRate * (Governor.Forsage ? 1.15f : 1f));
            }
            if (it.AltPressed && Core.CanFire && P.AltCd <= 0f) { Fire(3f, true); if (P.AbT <= 0f) Core.AddHeat(ShotHeat * 3f); P.AltCd = 0.8f; }
            if (!Core.CanFire) Governor.Forsage = false;

            // pickups
            for (int i = Pickups.Count - 1; i >= 0; i--)
            {
                var p = Pickups[i]; float dd = Dist(p.X, p.Y, P.X, P.Y);
                if (dd < 60f || p.Vacuum) { float a = Mathf.Atan2(P.Y - p.Y, P.X - p.X), s = p.Vacuum ? 620f : 360f; p.X += Mathf.Cos(a) * s * dt; p.Y += Mathf.Sin(a) * s * dt; }
                if (dd < P.R + 8f) { Pickups.RemoveAt(i); Collect(p); }
            }
            // leave through an open door at the top
            if (P.Y < Cell * 0.6f) for (int i = 0; i < Doors.Count; i++) { var d = Doors[i]; if (d.Open && P.X > d.X0 && P.X < d.X1) { LeaveRoom(i); return; } }
        }

        void Fire(float dmgMul, bool heavy)
        {
            float a = P.Aim + (_rngC.Next() - 0.5f) * 2f * ShotSpread;
            Shots.Add(new BomberShot
            {
                X = P.X + Mathf.Cos(P.Aim) * 14f, Y = P.Y + Mathf.Sin(P.Aim) * 14f, Vx = Mathf.Cos(a) * ShotSpeed * (heavy ? 1.1f : 1f), Vy = Mathf.Sin(a) * ShotSpeed * (heavy ? 1.1f : 1f),
                Dmg = ShotDamage * dmgMul * Core.DamageMultiplier, R = heavy ? 7f : 4f, Life = 1.4f, Heavy = heavy,
                Color = Core.State == CoreState.Overdrive ? new Color32(255, 227, 154, 255) : new Color32(216, 248, 255, 255),
            });
            P.MuzzleT = 0.06f;
            if (heavy) Shake?.Invoke(0.5f);
        }

        void Vent()
        {
            float hot = Core.Heat / 100f, vr = 64f + 60f * hot, vd = Mathf.Round(20f + 50f * hot * hot);
            Core.Vent(50f); P.TacCd = 8f; Shake?.Invoke(0.4f + 0.4f * hot);
            AddRing(P.X, P.Y, vr, hot > 0.7f ? new Color32(255, 179, 71, 255) : new Color32(127, 227, 255, 255), 0.35f);
            foreach (var e in Enemies.ToArray()) if (Dist(P, e) < vr + e.R) DamageEnemy(e, vd, Ang(P, e), 260f);
            foreach (var b in Shots) if (b.Enemy && Dist(b.X, b.Y, P.X, P.Y) < vr + 6f) b.Dead = true;
            if (hot > 0.6f) Texts.Add(new BomberText { X = P.X, Y = P.Y - 26, S = $"СБРОС {vd}", T = 0.8f, Color = new Color32(255, 179, 71, 255) });
        }

        void Emp()
        {
            P.Emp--;
            AddRing(P.X, P.Y, 160f, new Color32(159, 208, 255, 255), 0.5f);
            foreach (var e in Enemies) if (Dist(P, e) < 160f) e.Stun = 2f;
            foreach (var b in Shots) if (b.Enemy && Dist(b.X, b.Y, P.X, P.Y) < 160f) b.Dead = true;
            Toast?.Invoke("ЭМИ", 1f);
        }

        void Collect(BomberPickup p)
        {
            if (p.Kind == "scrap") P.Scrap += p.Value;
            if (p.Kind == "repair") { P.Hp = Mathf.Min(P.MaxHp, P.Hp + 1); Toast?.Invoke("+1 здоровье", 1f); }
            if (p.Kind == "emp") { P.Emp = Mathf.Min(3, P.Emp + 1); Toast?.Invoke("+1 ЭМИ", 1f); }
        }

        void Hurt()
        {
            if (P.ITime > 0f || Phase != BomberPhase.Play) return;
            P.Hp--; DamageTaken++; RoomDamaged = true; P.ITime = BomberData.IFrames; Shake?.Invoke(1f); Flash = 0.15f;
            if (Combo >= 5) Texts.Add(new BomberText { X = P.X, Y = P.Y - 30, S = "КОМБО ПОТЕРЯНО", T = 0.9f, Color = new Color32(255, 138, 154, 255) });
            Combo = 0; ComboT = 0f;
            for (int i = 0; i < 14; i++) Spark(P.X, P.Y, new Color32(255, 90, 110, 255), 200f);
            if (P.Hp <= 0) Phase = BomberPhase.Dead;
        }

        // ================= rooms & waves =================
        float HpMul => BomberData.HpMul[Sector];

        List<List<(string, bool)>> MakeWaves(string type)
        {
            int s = Sector;
            float mult = BomberData.SectorMult[s] * (type == "elite" ? 1.3f : 1f) * (type == "start" ? 0.6f : 1f) * (0.9f + _rngE.Next() * 0.3f);
            int budget = Mathf.Max(3, Mathf.RoundToInt(BomberData.Budget * mult));
            var pool = new List<string>(BomberData.Pools[0]);
            int n = budget < 8 ? 1 : budget < 14 ? 2 : 3;
            float[] split = n == 1 ? new[] { 1f } : n == 2 ? new[] { 0.6f, 0.4f } : new[] { 0.4f, 0.33f, 0.27f };
            float eliteChance = BomberData.EliteChance[s];
            var waves = new List<List<(string, bool)>>();
            for (int wi = 0; wi < split.Length; wi++)
            {
                int b = Mathf.Max(1, Mathf.RoundToInt(budget * split[wi])); var list = new List<(string, bool)>();
                if (wi == 0 && type == "elite") { var opts0 = pool.FindAll(t => t != "kamikaze"); var et = _rngE.Pick(opts0); list.Add((et, true)); b -= BomberData.Enemies[et].Cost * 2; }
                for (int guard = 0; b > 0 && guard < 80; guard++)
                {
                    var opts = pool.FindAll(t => BomberData.Enemies[t].Cost <= b);
                    if (opts.Count == 0) break;
                    var t = _rngE.Pick(opts);
                    list.Add((t, type != "start" && t != "kamikaze" && _rngE.Next() < eliteChance)); b -= BomberData.Enemies[t].Cost;
                }
                waves.Add(list);
            }
            return waves;
        }

        void SpawnWave(int i)
        {
            Room.Wave = i; Room.WaveT = 0f; Room.WaveHp = 0f;
            var free = FreeCells().FindAll(c => Dist(c.x * Cell + 16, c.y * Cell + 16, P.X, P.Y) > 230f);
            foreach (var (kind, elite) in Room.Waves[i])
            {
                var c = free.Count > 0 ? free[_rngE.Range(free.Count)] : (8, 12); free.Remove(c);
                Effects.Add(new BomberEffect { Kind = "spawn", X = c.Item1 * Cell + 16, Y = c.Item2 * Cell + 16, T = 0.6f, Max = 0.6f, Spawn = kind, Elite = elite });
                Room.WaveHp += BomberData.Enemies[kind].Hp * HpMul * (elite ? 1.5f : 1f);
            }
        }

        void UpdateRoom(float dt)
        {
            if (!Room.Locked || Room.Cleared) return;
            if (Room.StartDelay > 0f) { Room.StartDelay -= dt; if (Room.StartDelay <= 0f) SpawnWave(0); return; }
            Room.WaveT += dt;
            bool pending = Effects.Exists(e => e.Kind == "spawn");
            float cur = 0f; foreach (var e in Enemies) if (e.Wave == Room.Wave) cur += Mathf.Max(0f, e.Hp);
            if (Room.Wave < Room.Waves.Count - 1 && !pending && (cur <= Room.WaveHp * 0.35f || Room.WaveT > 10f)) SpawnWave(Room.Wave + 1);
            if (Room.Wave == Room.Waves.Count - 1 && !pending && Enemies.Count == 0) RoomCleared();
        }

        void RoomCleared()
        {
            if (Room.Cleared) return;
            Room.Cleared = true; Rooms++; Score += 100;
            if (!RoomDamaged) { Score += 50; Texts.Add(new BomberText { X = P.X, Y = P.Y - 24, S = "БЕЗ УРОНА +50", T = 1.4f, Color = new Color32(109, 255, 176, 255) }); }
            Texts.Add(new BomberText { X = W / 2f, Y = H / 2f - 40f, S = "КОМНАТА ЗАЧИЩЕНА", T = 1.2f, Color = new Color32(127, 227, 255, 255), Big = true });
            for (int k = 0; k < 5; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = W / 2f + (_rngC.Next() - 0.5f) * 60f, Y = H / 2f + (_rngC.Next() - 0.5f) * 60f });
            foreach (var p in Pickups) p.Vacuum = true;
            OpenDoors();
        }

        void LeaveRoom(int i)
        {
            RoomDamaged = false;
            Step++;
            EnterRoom(Doors[i].Type);
        }

        // ================= small helpers =================
        void Spark(float x, float y, Color32 c, float sp)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f, s = sp * (0.3f + UnityEngine.Random.value * 0.7f);
            Particles.Add(new BomberParticle { X = x, Y = y, Vx = Mathf.Cos(a) * s, Vy = Mathf.Sin(a) * s, Life = 0.3f + UnityEngine.Random.value * 0.35f, Max = 0.6f, Size = 2f + UnityEngine.Random.value * 2f, Color = c });
        }
        void Particle(float x, float y, float vx, float vy, float life, Color32 c, float size) => Particles.Add(new BomberParticle { X = x, Y = y, Vx = vx, Vy = vy, Life = life, Max = life, Size = size, Color = c });
        void AddRing(float x, float y, float r, Color32 c, float t) => Effects.Add(new BomberEffect { Kind = "ring", X = x, Y = y, R = r, T = t, Max = t, Color = c });
    }
}
