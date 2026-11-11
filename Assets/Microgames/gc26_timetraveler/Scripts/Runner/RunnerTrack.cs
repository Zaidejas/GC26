using System.Collections.Generic;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Greybox track for the endless test run. Builds obstacle patterns ahead of the camera and
    /// removes them behind it. New obstacle types unlock with distance, so each mechanic is met alone first:
    ///   0 m+   rock (jump), small gap, log (slide)
    ///   40+    steps, floating ledge, tall wall (double jump)
    ///   70+    world-only pieces: bridge over a wide gap, a wall that exists in one timeline, split path
    ///   110+   moving: bobbing blade, flyer, falling boulders, crusher (slide always works; time slow helps)
    /// Ground at the same height is merged into one long box, so the traveler never catches on seams.
    /// Later replaced by hand-made chunk prefabs with Tilemaps (see the GDD).
    /// </summary>
    public class RunnerTrack : MonoBehaviour
    {
        const float GroundDepth = 30f;
        const float MaxGroundY = 7.5f;
        const float StepHeight = 2.5f;
        const float SafeStartLength = 40f;
        /// <summary>Hazard hitboxes are this much smaller than their drawing on every side,
        /// so touching the very edge of a red block does not kill.</summary>
        const float HazardInset = 0.2f;

        // Distances (units from the start) where obstacle groups unlock.
        const float UnlockMedium = 40f / MetersPerUnit;
        const float UnlockWorlds = 70f / MetersPerUnit;
        const float UnlockMoving = 110f / MetersPerUnit;
        const float MetersPerUnit = RunnerSession.MetersPerUnit;

        public float KillY => -12f;
        public float StartX => 0f;
        /// <summary>X of the finish line once it is built (levels only); +infinity in endless mode.</summary>
        public float FinishX { get; private set; } = float.PositiveInfinity;

        const float CoinSize = 0.75f;

        // World pieces use the world's colour (same as the traveler in that world).
        // Solids are full colour; world walls are a darker shade of it.
        static Color SolidColor(RunnerWorld w) => RunnerWorldState.ColorOf(w);
        static Color WallColor(RunnerWorld w) => RunnerWorldState.ColorOf(w) * new Color(0.75f, 0.75f, 0.75f, 1f);

        /// <summary>First time each obstacle type appears in a run, a hint is shown before it.</summary>
        readonly List<(float x, string text)> _hints = new List<(float, string)>();
        readonly HashSet<string> _taught = new HashSet<string>();

        RunnerShapes _shapes;
        RunnerWorldState _worlds;
        Transform _target;
        System.Random _rng;
        float _cursorX;
        float _groundY;
        Transform _openGround;          // last ground box, extended while the height stays the same
        float _openGroundStartX;
        string _lastPattern;
        int _tier = -1;                 // levels: obstacle groups 0..tier; endless (-1): unlocked by distance
        float _density = 1f;            // levels: < 1 = less ground between patterns
        bool _hard;                     // levels: triple and mixed combos
        float _plannedFinishX = float.PositiveInfinity;
        bool _finishBuilt;
        readonly List<Transform> _pieces = new List<Transform>();
        /// <summary>Tests: every live track piece (ground, hazards, coins...).</summary>
        internal IReadOnlyList<Transform> Pieces => _pieces;
        // Pieces (solids and hazards) that exist in only one world, for the world-switch safety check.
        readonly List<RunnerWorldMember> _worldPieces = new List<RunnerWorldMember>();

        delegate void Pattern();

        /// <param name="tier">Levels: which obstacle groups appear (0 basic, 1 +steps/ledge/wall, 2 +worlds, 3 +moving).
        /// -1 = endless: groups unlock with distance.</param>
        /// <param name="finishMeters">Levels: distance of the finish line; 0 = endless.</param>
        public void Build(RunnerShapes shapes, RunnerWorldState worlds, Transform target, int seed,
            int tier = -1, float finishMeters = 0f, float density = 1f, bool hard = false)
        {
            _shapes = shapes;
            _worlds = worlds;
            _target = target;
            Clear();
            _rng = new System.Random(seed);
            _hints.Clear();
            _taught.Clear();
            _cursorX = StartX - 20f;
            _groundY = 0f;
            _lastPattern = null;
            _tier = tier;
            _density = density;
            _hard = hard;
            _finishBuilt = false;
            FinishX = float.PositiveInfinity;
            _plannedFinishX = finishMeters > 0f ? StartX + finishMeters / MetersPerUnit : float.PositiveInfinity;
            Flat(SafeStartLength);
        }

        public void Extend(float untilX)
        {
            while (_cursorX < untilX)
            {
                if (_cursorX < _plannedFinishX) AddPattern();
                else if (!_finishBuilt) BuildFinish();
                else Flat(20f);
            }
        }

        public void Trim(float behindX)
        {
            for (var i = _pieces.Count - 1; i >= 0; i--)
            {
                var piece = _pieces[i];
                if (piece == null) { _pieces.RemoveAt(i); continue; }
                if (piece == _openGround) continue;
                var right = piece.position.x + Mathf.Abs(piece.localScale.x) / 2f;
                if (right < behindX)
                {
                    var member = piece.GetComponent<RunnerWorldMember>();
                    if (member != null) _worldPieces.Remove(member);
                    Destroy(piece.gameObject);
                    _pieces.RemoveAt(i);
                }
            }
        }

        /// <summary>True if a piece (solid or hazard) that only exists in <paramref name="world"/> overlaps the box.
        /// Used to refuse a world switch that would put the traveler inside a wall (instead of killing him).</summary>
        public bool SolidOverlaps(Bounds box, RunnerWorld world)
        {
            foreach (var member in _worldPieces)
            {
                if (member == null || member.World != world) continue;
                var t = member.transform;
                var b = new Bounds(t.position, new Vector3(Mathf.Abs(t.localScale.x), Mathf.Abs(t.localScale.y), 1f));
                if (b.Intersects(box)) return true;
            }
            return false;
        }

        /// <summary>The hint for the next taught obstacle, if the traveler is approaching it.</summary>
        public string HintFor(float travelerX)
        {
            foreach (var (x, text) in _hints)
            {
                var ahead = x - travelerX;
                if (ahead > -1f && ahead < 18f) return text;
            }
            return null;
        }

        /// <summary>Adds a hint at the current build position, once per obstacle type per run.</summary>
        void Teach(string key, string text)
        {
            if (!_taught.Add(key)) return;
            _hints.Add((_cursorX, text));
        }

        static string Name(RunnerWorld w) => RunnerWorldState.NameOf(w);
        static string Other(RunnerWorld w) => RunnerWorldState.NameOf(w == RunnerWorld.Past ? RunnerWorld.Future : RunnerWorld.Past);

        void Clear()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);
            _pieces.Clear();
            _worldPieces.Clear();
            _openGround = null;
        }

        float Distance => _cursorX - StartX;
        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        bool Coin() => _rng.NextDouble() < 0.5;
        RunnerWorld RandomWorld() => Coin() ? RunnerWorld.Past : RunnerWorld.Future;

        /// <summary>Levels unlock groups by tier; endless unlocks them by distance.</summary>
        bool Unlocked(int group, float endlessDistance) =>
            _tier >= 0 ? _tier >= group : Distance > endlessDistance;

        void AddPattern()
        {
            // Weighted list of what is unlocked at this distance.
            // In a level, the level's newest group is featured and plain flat ground is gone after level 1.
            var options = new List<(string name, Pattern build, float weight)>
            {
                ("rock", Rock, 2f),
                ("gap", () => Gap(Range(3f, 4.5f)), 1.6f),
                ("log", Log, 1.6f),
            };
            if (_tier <= 0) options.Add(("flat", () => Flat(Range(6f, 9f)), 0.5f));
            if (Unlocked(1, UnlockMedium))
            {
                options.Add(("stepup", StepUp, 1f));
                options.Add(("stepdown", StepDown, 1f));
                options.Add(("ledge", Ledge, 1f));
                options.Add(("tallwall", TallWall, 1.2f));
                // Combos: two basic obstacles back to back (12 units apart: enough even at top speed).
                options.Add(("rockrock", RockRock, 1.1f));
                options.Add(("rocklog", RockLog, 1.1f));
                options.Add(("gaprock", GapRock, 1f));
                options.Add(("logrock", LogRock, 1f));
            }
            if (_hard)
            {
                options.Add(("rock3", RockRockRock, 1.2f));
                options.Add(("worldwallrock", WorldWallRock, 1.2f));
            }
            if (Unlocked(2, UnlockWorlds))
            {
                options.Add(("worldbridge", WorldBridge, 1.6f));
                options.Add(("worldwall", WorldWall, 1.6f));
                options.Add(("worldsplit", WorldSplit, 1.2f));
            }
            if (Unlocked(3, UnlockMoving))
            {
                options.Add(("blade", Blade, 1.3f));
                options.Add(("flyer", Flyer, 1.3f));
                options.Add(("boulders", Boulders, 1.1f));
                options.Add(("crusher", Crusher, 1.1f));
            }

            if (_tier >= 1)
                for (var i = 0; i < options.Count; i++)
                    if (GroupOf(options[i].name) == _tier)
                        options[i] = (options[i].name, options[i].build, options[i].weight * 1.6f);

            float total = 0f;
            foreach (var o in options) if (o.name != _lastPattern) total += o.weight;
            var roll = (float)_rng.NextDouble() * total;
            foreach (var o in options)
            {
                if (o.name == _lastPattern) continue;
                roll -= o.weight;
                if (roll > 0f) continue;
                _lastPattern = o.name;
                o.build();
                // Breathing room between patterns, shrinking as the run goes on, sometimes with coins.
                var tighten = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(Distance / 600f));
                var rest = Range(2f, 4.5f) * tighten * _density;
                if (rest > 3f && Coin()) CoinRow(_cursorX + 0.5f, _groundY + 1.5f, 3, 1.3f);
                Ground(Mathf.Max(1.5f, rest));   // every pattern already starts and ends with its own run-up
                return;
            }
        }

        // ---------- basic patterns ----------

        void Flat(float length) => Ground(Mathf.Max(3f, length));

        void Gap(float width)
        {
            Flat(5f);
            Teach("gap", "Gap ahead: JUMP  (tap / Space)");
            CoinArc(_cursorX + width / 2f, _groundY);
            _cursorX += width;
            _openGround = null;
            Flat(7f);
        }

        void Rock()
        {
            Flat(4f);
            Teach("rock", "Red = deadly. JUMP over the rock  (tap / Space)");
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            CoinArc(_cursorX + 0.75f, _groundY);
            Flat(6f);
        }

        void Log()
        {
            // Overhead log: standing (3 units) hits it, sliding (1.2 units) passes under.
            Flat(4f);
            Teach("log", "SLIDE under the log  (swipe down / hold S)");
            Hazard("TrackLog", _cursorX, _groundY + 2.0f, 2.5f, 14f, TimetravelerArt.Log);
            CoinRow(_cursorX - 1f, _groundY + 0.6f, 4, 1.2f);   // low coins reward the slide
            Flat(6f);
        }

        static int GroupOf(string pattern)
        {
            switch (pattern)
            {
                case "stepup": case "stepdown": case "ledge": case "tallwall":
                case "rockrock": case "rocklog": case "gaprock": case "logrock": return 1;
                case "worldbridge": case "worldwall": case "worldsplit": case "worldwallrock": return 2;
                case "blade": case "flyer": case "boulders": case "crusher": return 3;
                default: return 0;
            }
        }

        // ---------- combos ----------

        const float ComboGap = 12f;

        void RockRock()
        {
            Flat(4f);
            Teach("rockrock", "Two rocks: jump, land, jump again");
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            CoinArc(_cursorX + 0.75f, _groundY);
            Flat(ComboGap);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            CoinArc(_cursorX + 0.75f, _groundY);
            Flat(6f);
        }

        void RockLog()
        {
            Flat(4f);
            Teach("rocklog", "Jump the rock, then SLIDE (hold S / swipe down already in the air)");
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            Flat(ComboGap);
            Hazard("TrackLog", _cursorX, _groundY + 2.0f, 2.5f, 14f, TimetravelerArt.Log);
            CoinRow(_cursorX - 1f, _groundY + 0.6f, 4, 1.2f);
            Flat(6f);
        }

        void LogRock()
        {
            // Slide under, stand up, jump: jumping is allowed as soon as nothing is overhead.
            Flat(4f);
            Teach("logrock", "Slide under the log, then JUMP the rock");
            Hazard("TrackLog", _cursorX, _groundY + 2.0f, 2.5f, 14f, TimetravelerArt.Log);
            CoinRow(_cursorX - 1f, _groundY + 0.6f, 4, 1.2f);
            Flat(ComboGap + 2.5f);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            CoinArc(_cursorX + 0.75f, _groundY);
            Flat(6f);
        }

        void RockRockRock()
        {
            Flat(4f);
            Teach("rock3", "Three rocks! Jump, jump, jump");
            for (var i = 0; i < 3; i++)
            {
                Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
                CoinArc(_cursorX + 0.75f, _groundY);
                Flat(i < 2 ? ComboGap : 6f);
            }
        }

        void WorldWallRock()
        {
            // Switch through a one-world wall, then jump a rock right behind it.
            var world = RandomWorld();
            Flat(9f);
            Teach("worldwallrock", $"Switch to the {Other(world)} for the wall, then JUMP");
            WorldHazard("TrackWorldWall", world, _cursorX, _groundY, 1.5f, 16f);
            Flat(ComboGap);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            Flat(6f);
        }

        void GapRock()
        {
            Flat(4f);
            var width = Range(3f, 4f);
            CoinArc(_cursorX + width / 2f, _groundY);
            _cursorX += width;
            _openGround = null;
            Flat(ComboGap);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
            Flat(6f);
        }

        void StepUp()
        {
            if (_groundY + StepHeight > MaxGroundY) { StepDown(); return; }
            Flat(5f);
            _groundY += StepHeight;
            _openGround = null;
            Flat(9f);
        }

        void StepDown()
        {
            if (_groundY - StepHeight < 0f) { Rock(); return; }
            Flat(5f);
            _groundY -= StepHeight;
            _openGround = null;
            Flat(9f);
        }

        void Ledge()
        {
            // A one-way floating platform above a rock: jump onto it, or jump the rock below.
            var startX = _cursorX;
            Flat(16f);
            OneWayPlatform(startX + 3f, _groundY + 4.5f, 9f, RunnerShapes.LedgeColor);
            Hazard("TrackRock", startX + 9f, _groundY, 1.5f, 1.5f, TimetravelerArt.Rock);
        }

        void TallWall()
        {
            // A double-jump wall: one jump only just scrapes over it, two jumps clear it easily.
            Flat(6f);
            Teach("tallwall", "High wall: jump TWICE  (tap, tap / Space, Space)");
            Hazard("TrackTallWall", _cursorX, _groundY, 1.2f, 4.5f, TimetravelerArt.TallWall);
            // A double jump at top speed carries the traveler ~13 units past the wall: land on clear ground.
            Flat(16f);
        }

        // ---------- world patterns ----------

        void WorldBridge()
        {
            // A gap too wide to jump; a bridge spans it in only one timeline.
            var world = RandomWorld();
            Flat(6f);
            Teach("worldbridge", $"Too wide to jump! The bridge is real only in the {Name(world)}.\nBe {Name(world)}-coloured to run on it  (Q / swipe sideways)");
            var width = Range(13f, 16f);
            WorldSolid("TrackBridge", world, _cursorX, _groundY - 1f, width, 1f);
            CoinRow(_cursorX + 2f, _groundY + 1.5f, 5, 2f);   // coins over the bridge: only for the right world
            _cursorX += width;
            _openGround = null;
            Flat(8f);
        }

        void WorldWall()
        {
            // A wall too tall to jump that exists in only one timeline: be in the other one.
            var world = RandomWorld();
            Flat(9f);
            Teach("worldwall", $"This wall exists only in the {Name(world)}.\nSwitch to the {Other(world)} to run through it  (Q / swipe sideways)");
            WorldHazard("TrackWorldWall", world, _cursorX, _groundY, 1.5f, 16f);
            Flat(9f);
        }

        void WorldSplit()
        {
            // Same spot, different obstacle in each timeline: an unjumpable wall in one,
            // a log to slide under in the other.
            var wallWorld = RandomWorld();
            var logWorld = wallWorld == RunnerWorld.Past ? RunnerWorld.Future : RunnerWorld.Past;
            Flat(9f);
            Teach("worldsplit", $"Wall in the {Name(wallWorld)}, log in the {Name(logWorld)}.\nSwitch to the {Name(logWorld)} and SLIDE");
            WorldHazard("TrackSplitWall", wallWorld, _cursorX, _groundY, 1.5f, 16f);
            WorldHazard("TrackSplitLog", logWorld, _cursorX - 0.5f, _groundY + 2.0f, 2.5f, 14f);
            Flat(9f);
        }

        // ---------- moving patterns ----------

        void Blade()
        {
            // Bobs between head height and high above. At its lowest it is still above a sliding
            // traveler, so SLIDE always works; standing works only while it is up.
            Flat(8f);
            Teach("blade", "Moving blade: SLIDE under it  (or run while it is UP)");
            var blade = Hazard("TrackBlade", _cursorX, _groundY + 2f, 1.5f, 1.5f, TimetravelerArt.Blade);
            AddMover(blade, RunnerMover.Mode.Bob, low: _groundY + 2.6f, high: _groundY + 10f, period: 2.4f);
            Flat(10f);
        }

        void Flyer()
        {
            // Flies at head height toward the traveler: slide under it (or jump over early).
            Flat(20f);
            Teach("flyer", "Flyer at head height: SLIDE under it  (or slow time: E / Shift)");
            var flyer = Hazard("TrackFlyer", _cursorX, _groundY + 2.1f, 2.2f, 1.2f, TimetravelerArt.Flyer);
            var mover = AddMover(flyer, RunnerMover.Mode.Fly, 0f, 0f, 1f);
            mover.speed = 5f;
            mover.triggerDistance = 22f;
            Flat(6f);
        }

        void Boulders()
        {
            // Two or three boulders fall when the traveler gets close (they flash first).
            var count = Coin() ? 2 : 3;
            Flat(6f);
            Teach("boulders", "Boulders fall ahead of you. JUMP over them when they land");
            for (var i = 0; i < count; i++)
            {
                var boulder = Hazard("TrackBoulder", _cursorX, _groundY + 18f, 1.8f, 1.8f, TimetravelerArt.Boulder);
                var mover = AddMover(boulder, RunnerMover.Mode.Drop, low: _groundY + 0.9f, high: 0f, period: 1f);
                // Falls early enough to be on the ground well before the traveler gets there.
                mover.triggerDistance = 22f;
                Flat(9f);
            }
            Flat(4f);
        }

        void Crusher()
        {
            // Slams down to just above a sliding traveler and slowly rises: SLIDE always works,
            // running upright works only while it is up.
            Flat(8f);
            Teach("crusher", "Crusher: SLIDE under it  (or run while it is UP)");
            var crusher = Hazard("TrackCrusher", _cursorX, _groundY + 2f, 3f, 8f, TimetravelerArt.Crusher);
            AddMover(crusher, RunnerMover.Mode.Crush, low: _groundY + 5.8f, high: _groundY + 13f, period: 2.4f,
                phase: (float)_rng.NextDouble());
            Flat(10f);
        }

        // ---------- building blocks ----------

        void Ground(float length)
        {
            var top = _groundY;
            if (_openGround != null)
            {
                // Same height as the last box: stretch it instead of adding a seam.
                var width = _cursorX + length - _openGroundStartX;
                _openGround.localScale = new Vector3(width, GroundDepth, 1f);
                _openGround.position = new Vector3(_openGroundStartX + width / 2f, top - GroundDepth / 2f, 0f);
            }
            else
            {
                var box = _shapes.Box(transform, "TrackGround", _cursorX, top - GroundDepth, length, GroundDepth,
                    RunnerShapes.GroundColor, slot: TimetravelerArt.Ground, tile: true);
                _openGround = box.transform;
                _openGroundStartX = _cursorX;
                _pieces.Add(box.transform);
            }
            _cursorX += length;
        }

        GameObject Hazard(string name, float x, float y, float width, float height, string slot = null)
        {
            var go = _shapes.Box(transform, name, x, y, width, height, RunnerShapes.HazardColor, isTrigger: true,
                sortingOrder: 1, slot: slot);
            go.AddComponent<RunnerHazard>();
            Inset(go, width, height);
            _pieces.Add(go.transform);
            return go;
        }

        void OneWayPlatform(float x, float y, float width, Color color)
        {
            var go = _shapes.Box(transform, "TrackLedge", x, y, width, 1f, color, slot: TimetravelerArt.Ledge, tile: true);
            // Solid only from above: no deaths from bumping its side or underside.
            go.GetComponent<BoxCollider2D>().usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            _pieces.Add(go.transform);
        }

        void WorldSolid(string name, RunnerWorld world, float x, float y, float width, float height)
        {
            var color = SolidColor(world);
            var go = _shapes.Box(transform, name, x, y, width, height, color, slot: TimetravelerArt.Bridge, tile: true,
                tint: true);
            var member = go.AddComponent<RunnerWorldMember>();
            member.Init(world, _worlds);
            _worldPieces.Add(member);
            _pieces.Add(go.transform);
        }

        void WorldHazard(string name, RunnerWorld world, float x, float y, float width, float height)
        {
            var color = WallColor(world);
            var go = _shapes.Box(transform, name, x, y, width, height, color, isTrigger: true, sortingOrder: 1,
                slot: TimetravelerArt.WorldWall, tile: true, tint: true);
            go.AddComponent<RunnerHazard>();
            Inset(go, width, height);
            var member = go.AddComponent<RunnerWorldMember>();
            member.Init(world, _worlds);
            _worldPieces.Add(member);
            _pieces.Add(go.transform);
        }

        /// <summary>Shrinks a hazard's hitbox by HazardInset on each side (the drawing stays full size).</summary>
        static void Inset(GameObject go, float width, float height)
        {
            var col = go.GetComponent<BoxCollider2D>();
            col.size = new Vector2(Mathf.Max(0.2f, (width - 2f * HazardInset) / width),
                Mathf.Max(0.2f, (height - 2f * HazardInset) / height));
        }

        // ---------- coins and finish ----------

        static readonly Color CoinColor = new Color(1f, 0.85f, 0.25f);
        static readonly Color FinishColor = new Color(0.95f, 0.95f, 0.95f);

        void CoinAt(float centerX, float centerY)
        {
            var go = _shapes.Box(transform, "TrackCoin", centerX - CoinSize / 2f, centerY - CoinSize / 2f, CoinSize, CoinSize,
                CoinColor, isTrigger: true, sortingOrder: 2, slot: TimetravelerArt.Coin);
            go.AddComponent<RunnerCoin>();
            _pieces.Add(go.transform);
        }

        /// <summary>A straight row of coins (centre heights).</summary>
        void CoinRow(float startX, float centerY, int count, float spacing)
        {
            for (var i = 0; i < count; i++) CoinAt(startX + i * spacing, centerY);
        }

        // Coin heights along a full jump (the 3-unit tall body passes through all of them).
        static readonly float[] ArcHeights = { 2.5f, 6f, 7f, 6f, 2.5f };

        /// <summary>Five coins along the path of a jump centred on <paramref name="centerX"/>.</summary>
        void CoinArc(float centerX, float groundY)
        {
            for (var i = 0; i < ArcHeights.Length; i++)
                CoinAt(centerX + (i - 2) * 1.3f, groundY + ArcHeights[i]);
        }

        void BuildFinish()
        {
            Flat(4f);
            FinishX = _cursorX;
            _finishBuilt = true;
            // A tall gate the traveler runs through (no collider effect: it is a trigger with no component).
            _pieces.Add(_shapes.Box(transform, "TrackFinish", _cursorX, _groundY, 2f, 6f, FinishColor, isTrigger: true,
                sortingOrder: 0, slot: TimetravelerArt.Finish).transform);
            Flat(40f);
        }

        RunnerMover AddMover(GameObject go, RunnerMover.Mode mode, float low, float high, float period, float phase = 0f)
        {
            go.AddComponent<Rigidbody2D>();
            var mover = go.AddComponent<RunnerMover>();
            mover.mode = mode;
            mover.lowY = low;
            mover.highY = high;
            mover.period = period;
            mover.phase = phase;
            mover.Init(_target);
            return mover;
        }
    }
}
