using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    public enum BomberMode { Run, Hub, Tutorial }

    public sealed class BomberNpc { public string Id; public float X, Y, Tx, Ty, T; }

    // Hub between runs, the first-launch tutorial, survivors, achievements and the end of a run (Data reward).
    public sealed partial class BomberWorld
    {
        public BomberMode Mode { get; private set; } = BomberMode.Run;
        /// <summary>Progress between runs. BomberContext loads it and saves it on MetaChanged.</summary>
        public BomberMeta Meta = new BomberMeta();
        public event Action MetaChanged;
        public readonly List<BomberNpc> Npcs = new List<BomberNpc>();
        /// <summary>Hub terminal or survivor the player just used ("chars", "armory", ..., "npc:doctor"); the UI opens it and clears it.</summary>
        public string PendingPanel;
        public BomberSectorDef Theme => Mode == BomberMode.Hub ? HubTheme : BomberData.Sectors[Sector];
        static readonly BomberSectorDef HubTheme = new BomberSectorDef { Name = "Хаб", Tint = BomberData.C("#081014"), Floor = BomberData.C("#0c1a1e"), Accent = BomberData.C("#3a8a8a") };

        // end-of-run summary for the UI
        public int LastDataGain { get; private set; }
        public bool LastAbandoned { get; private set; }
        public bool LastBest { get; private set; }
        public readonly List<string> NewAch = new List<string>(), RescuedNow = new List<string>();
        public int Elites { get; private set; }
        readonly string[] _rescueFor = new string[3];
        bool _ended, _bossDamaged;

        void SaveMeta() => MetaChanged?.Invoke();

        // ================= achievements & survivors =================
        void Unlock(string id)
        {
            if (Meta.HasAch(id)) return;
            BomberAchDef a = null; foreach (var x in BomberMetaData.Achs) if (x.Id == id) a = x;
            if (a == null) return;
            Meta.Ach.Add(id); Meta.Data += a.Data; NewAch.Add(id); SaveMeta(); Sound("ach");
            Toast?.Invoke($"Достижение: {a.Name}. Награда: {a.Reward}", 3.5f);
        }

        void PlanRescues()
        {
            var queue = new List<string>();
            foreach (var n in BomberMetaData.Npcs) if (!n.Home && !Meta.IsRescued(n.Id)) queue.Add(n.Id);
            new BomberRng(BomberRng.Hash(Seed, 91u)).Shuffle(queue);
            for (int s = 0; s < 3; s++)
            {
                _rescueFor[s] = null;
                var r = new BomberRng(BomberRng.Hash(Seed, (uint)(200 + s)));
                if (queue.Count == 0 || r.Next() >= 0.7f) continue;
                var step = Plan[s][r.Next() < 0.5f ? 2 : 4]; int i = step.IndexOf("combat");
                if (i < 0) continue;
                step[i] = "rescue"; _rescueFor[s] = queue[0]; queue.RemoveAt(0);
            }
        }

        void Rescue(BomberProp cell)
        {
            var d = BomberMetaData.Npc(cell.Id); cell.Opened = true;
            if (!Meta.IsRescued(d.Id)) Meta.Rescued.Add(d.Id);
            RescuedNow.Add(d.Id); SaveMeta(); Sound("ach");
            for (int i = 0; i < 30; i++) Spark(cell.X, cell.Y, d.Color, 260f);
            Toast?.Invoke($"Спасён: {d.Name}. {d.Bonus}. Ждёт тебя в хабе.", 3.6f);
            if (Meta.Rescued.Count >= 3) Unlock("rescue3");
        }

        // ================= end of run =================
        void EndRun(bool win, bool abandoned = false)
        {
            if (_ended || Mode != BomberMode.Run) return;
            _ended = true; LastAbandoned = abandoned;
            if (win) { int bonus = Time < 720f ? Mathf.RoundToInt(2000f * (1f - Time / 720f)) : 0; Score += 3000 + bonus; Meta.Wins++; Unlock("win"); Meta.OlMax = Mathf.Max(Meta.OlMax, Mathf.Min(5, Ol + 1)); }
            int gain = Mathf.RoundToInt((Rooms * 2 + Elites * 4 + Bosses * 20 + Score / 400) * (1f + 0.25f * Ol) * (Meta.IsRescued("archivist") ? 1.1f : 1f) * (abandoned ? 0.5f : 1f));
            LastDataGain = gain; Meta.Data += gain; Meta.DataEarned += gain; Meta.BestCombo = Mathf.Max(Meta.BestCombo, MaxCombo);
            LastBest = Score > Meta.Best; if (LastBest) Meta.Best = Score;
            SaveMeta();
            Phase = win ? BomberPhase.Victory : BomberPhase.Dead;
        }

        /// <summary>Pause menu: give up the run (half the Data).</summary>
        public void Abandon() { if (Mode == BomberMode.Run && Phase == BomberPhase.Play) EndRun(false, true); }

        void OnOverheat() { Sound("over"); if (Mode != BomberMode.Run) return; Meta.Overheats++; if (Meta.Overheats >= 50) Unlock("over50"); }

        // ================= hub =================
        public void EnterHub()
        {
            Mode = BomberMode.Hub; Phase = BomberPhase.Play; Reward = null; BenchOpen = false; PendingPanel = null; _later.Clear();
            Seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            ResetRngs(0);
            Room = new BomberRoomState { Type = "hub", Fade = 0.35f };
            ClearRoom();
            Walls.Add(Rect(0, 0, 7, 1)); Walls.Add(Rect(10, 0, 7, 1)); Walls.Add(Rect(0, GH - 1, GW, 1)); Walls.Add(Rect(0, 1, 1, GH - 2)); Walls.Add(Rect(GW - 1, 1, 1, GH - 2));
            Doors.Add(new BomberDoor { Type = "launch", X0 = 7 * Cell, X1 = 10 * Cell, Open = true, Blocker = new BomberWall { On = false } });
            foreach (var s in BomberMetaData.Stations) Props.Add(new BomberProp { Kind = "station", Id = s.Id, X = s.X, Y = s.Y });
            int i = 0;
            foreach (var n in BomberMetaData.Npcs)
            {
                if (!(n.Home || Meta.IsRescued(n.Id)) || (n.Char && Meta.Char == n.Id)) continue;
                Npcs.Add(n.Home ? new BomberNpc { Id = n.Id, X = W / 2f, Y = 380f } : new BomberNpc { Id = n.Id, X = 230f + (i * 61) % 120, Y = 300f + (i * 137) % 420 });
                i++;
            }
            ComputeBlocked();
            SetupPlayer(); P.R = 9f; P.X = W / 2f; P.Y = H - 140f; P.Vx = P.Vy = 0f; P.DashT = 0f; P.ITime = 0f;
            Core.Vent(HeatModel.Max); Governor.Forsage = false;
            ComputeFlow(); RoomVersion++;
        }

        void TickHub(float dt, BomberIntent it)
        {
            UpdatePlayer(dt, it);
            foreach (var n in Npcs)
            {
                n.T -= dt;
                if (n.T <= 0f) { n.T = 2f + UnityEngine.Random.value * 3f; n.Tx = Mathf.Clamp(n.X + (UnityEngine.Random.value - 0.5f) * 140f, 200f, 344f); n.Ty = Mathf.Clamp(n.Y + (UnityEngine.Random.value - 0.5f) * 220f, 180f, 780f); }
                float d0 = Dist(n.Tx, n.Ty, n.X, n.Y); if (d0 > 4f && n.Tx > 0f) { n.X += (n.Tx - n.X) / d0 * 40f * dt; n.Y += (n.Ty - n.Y) / d0 * 40f * dt; }
            }
            for (int i = Particles.Count - 1; i >= 0; i--) { var p = Particles[i]; p.Life -= dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vx *= 0.9f; p.Vy *= 0.9f; if (p.Life <= 0f) Particles.RemoveAt(i); }
            for (int i = Effects.Count - 1; i >= 0; i--) { Effects[i].T -= dt; if (Effects[i].T <= 0f) Effects.RemoveAt(i); }
        }

        public BomberNpc NearNpc()
        {
            BomberNpc best = null; float bd = 44f;
            foreach (var n in Npcs) { float d0 = Dist(n.X, n.Y, P.X, P.Y); if (d0 < bd) { best = n; bd = d0; } }
            return best;
        }

        /// <summary>Player bought something in the hub: re-apply drone stats.</summary>
        public void RefreshHubPlayer() { if (Mode != BomberMode.Hub) return; float x = P.X, y = P.Y; SetupPlayer(); P.X = x; P.Y = y; }

        // ---- hub purchases (return false when there is not enough Data) ----
        bool Spend(int cost) { if (Meta.Data < cost) return false; Meta.Data -= cost; Sound("buy"); SaveMeta(); RefreshHubPlayer(); return true; }

        public bool SelectChar(string id) { if (!Meta.CharUnlocked(id)) return false; Meta.Char = id; SaveMeta(); RefreshHubPlayer(); return true; }
        public bool BuyChar(string id) { var c = BomberMetaData.Char(id); if (c.Id != id || Meta.CharUnlocked(id) || c.Cost == 0 || c.Rescue || c.Ach != null) return false; if (!Spend(c.Cost)) return false; Meta.Chars.Add(id); return SelectChar(id); }
        public int CharUpPrice(string ch, string k) { foreach (var u in BomberMetaData.CharUps) if (u.Id == k) { int l = Meta.CharUp(ch, k); return l < u.Cost.Length ? u.Cost[l] : -1; } return -1; }
        public bool BuyCharUp(string ch, string k) { int p = CharUpPrice(ch, k); if (p < 0 || !Meta.CharUnlocked(ch) || Meta.Data < p) return false; Meta.AddCharUp(ch, k); return Spend(p); }
        public int MetaUpPrice(string id) { foreach (var u in BomberMetaData.MetaUps) if (u.Id == id) { int l = Meta.Up(id); return l < u.Cost.Length ? u.Cost[l] : -1; } return -1; }
        public bool BuyMetaUp(string id) { int p = MetaUpPrice(id); if (p < 0 || Meta.Data < p) return false; Meta.AddUp(id); return Spend(p); }
        public bool BuyWeapon(string id) { if (Meta.WeaponUnlocked(id) || !BomberMetaData.WeaponUnlock.TryGetValue(id, out var u) || u.ach != null || Meta.Data < u.cost) return false; Meta.Weapons.Add(id); return Spend(u.cost); }
        public bool SetStartWeapon(string id) { if (!Meta.WeaponUnlocked(id)) return false; Meta.StartWeapon = id; SaveMeta(); RefreshHubPlayer(); return true; }

        // ================= tutorial =================
        public int TutStep { get; private set; }
        public const int TutSteps = 8;
        float _tutMoved, _tutWait; bool _tutDashed, _tutSpawned;

        public string TutText => TutStep switch
        {
            0 => "Джойстик справа: проведи пальцем по правой половине экрана, чтобы лететь.",
            1 => "Кнопка РЫВОК — короткий бросок с неуязвимостью. Им уходят от пуль. Нажми её.",
            2 => "Стрельба автоматическая: дрон сам целится в ближайшего врага. Уничтожь жуков.",
            3 => "Каждый выстрел греет ядро. Автоогонь держит его в безопасной зоне. СБРОС снимает 50 нагрева и бьёт волной. Нажми.",
            4 => "ФОРСАЖ разгоняет ядро: урон +30%, но можно перегреться и остаться без оружия. Включи его.",
            5 => "Ядро горячее! Нажми ФОРСАЖ ещё раз, чтобы вернуться в безопасную зону.",
            6 => "РАЗГОН — способность дрона: 5 с выстрелы не греют ядро. Нажми и разнеси волну врагов.",
            _ => "Отлично! Подбирай скрап и лети в дверь наверху — там хаб, оттуда начинаются забеги.",
        };
        /// <summary>Button the tutorial points at right now (the UI makes it pulse).</summary>
        public string TutButton => TutStep switch { 1 => "dash", 3 => "tac", 4 => "od", 5 => "od", 6 => "ab", _ => null };
        /// <summary>Buttons are introduced one by one during the tutorial.</summary>
        public bool TutShows(string id) => Mode != BomberMode.Tutorial || id switch { "dash" => TutStep >= 1, "tac" => TutStep >= 3, "od" => TutStep >= 4, "ab" => TutStep >= 6, "emp" => false, "alt" => false, "swap" => false, _ => true };

        public void EnterTutorial()
        {
            Mode = BomberMode.Tutorial; Phase = BomberPhase.Play; Reward = null; BenchOpen = false; _later.Clear();
            Seed = 777u; Sector = 0; Step = 0; ResetRngs(0);
            Score = Kills = Rooms = Combo = MaxCombo = DamageTaken = 0; ComboT = Time = 0f;
            Room = new BomberRoomState { Type = "tutorial", Fade = 0.35f };
            ClearRoom();
            BuildBorder(new List<string> { "hub" });
            Walls.Add(Rect(3, 10, 2, 2, cover: true)); Walls.Add(Rect(12, 10, 2, 2, cover: true)); Walls.Add(Rect(7, 17, 3, 1, cover: true));
            ComputeBlocked();
            var keepChar = Meta.Char; var keepTac = Meta.Tactic; Meta.Char = "spark"; Meta.Tactic = "vent"; SetupPlayer(); Meta.Char = keepChar; Meta.Tactic = keepTac;
            P.Weapons.Clear(); P.Weapons.Add(new BomberWeapon { Id = "blaster" }); P.Mods.Clear(); P.Comps.Clear(); P.Emp = 1; P.Scrap = 0;
            P.R = 9f; P.X = W / 2f; P.Y = H - 120f; P.Vx = P.Vy = 0f; P.DashT = 0f; P.ITime = 0f; P.AbT = P.AbCd = P.TacCd = 0f;
            Core.Vent(HeatModel.Max); Governor.Forsage = false; ApplyCoreMods();
            TutStep = 0; _tutMoved = 0f; _tutWait = 0f; _tutDashed = false; _tutSpawned = false;
            ComputeFlow(); RoomVersion++;
            Toast?.Invoke("Обучение. Его можно пропустить кнопкой сверху.", 2.5f);
        }

        void TutSpawn(params string[] kinds)
        {
            var fc = FreeCells().FindAll(c => Dist(c.x * Cell + 16f, c.y * Cell + 16f, P.X, P.Y) > 200f && c.y < 16);
            foreach (var k in kinds) { if (fc.Count == 0) break; int i = _rngE.Range(fc.Count); var c = fc[i]; fc.RemoveAt(i); Effects.Add(new BomberEffect { Kind = "spawn", X = c.x * Cell + 16f, Y = c.y * Cell + 16f, T = 0.6f, Max = 0.6f, Spawn = k }); }
            _tutSpawned = true;
        }

        bool TutCleared => _tutSpawned && Enemies.Count == 0 && !Effects.Exists(e => e.Kind == "spawn");

        void TickTutorial(float dt, BomberIntent it)
        {
            if (_tutWait > 0f) { _tutWait -= dt; if (_tutWait <= 0f) BeginTutStep(); return; }
            bool done = false;
            switch (TutStep)
            {
                case 0: _tutMoved += Mathf.Sqrt(P.Vx * P.Vx + P.Vy * P.Vy) * dt; done = _tutMoved > 260f; break;
                case 1: if (P.DashT > 0f) _tutDashed = true; done = _tutDashed && P.DashT <= 0f; break;
                case 2: done = TutCleared; break;
                case 3: done = P.TacCd > 0f; break;
                case 4: done = Governor.Forsage; break;
                case 5: done = !Governor.Forsage; break;
                case 6: done = P.AbCd > 0f && TutCleared; break;
            }
            if (done) { TutStep++; _tutWait = 0.7f; Toast?.Invoke("Готово!", 0.8f); }
        }

        void BeginTutStep()
        {
            _tutSpawned = false;
            switch (TutStep)
            {
                case 2: TutSpawn("beetle", "beetle", "beetle"); break;
                case 3: if (Core.Heat < 70f) Core.AddHeat(70f - Core.Heat); break;
                case 6: TutSpawn("beetle", "beetle", "scout", "turret", "beetle"); break;
                case 7: OpenDoors(); for (int k = 0; k < 6; k++) Pickups.Add(new BomberPickup { Kind = "scrap", Value = 2, X = W / 2f + (_rngC.Next() - 0.5f) * 120f, Y = H / 2f + (_rngC.Next() - 0.5f) * 120f }); break;
            }
        }

        public void SkipTutorial() { if (Mode == BomberMode.Tutorial) CompleteTutorial(); }

        void CompleteTutorial()
        {
            Meta.TutorialDone = true; SaveMeta();
            EnterHub();
            Toast?.Invoke("Добро пожаловать в хаб. Подойди к терминалам и нажми E. Дверь наверху — начать забег.", 4f);
        }
    }
}
