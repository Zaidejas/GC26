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
    ///   110+   moving: bobbing blade, flyer, falling boulders, crusher (time slow helps)
    /// Ground at the same height is merged into one long box, so the traveler never catches on seams.
    /// Later replaced by hand-made chunk prefabs with Tilemaps (see the GDD).
    /// </summary>
    public class RunnerTrack : MonoBehaviour
    {
        const float GroundDepth = 30f;
        const float MaxGroundY = 7.5f;
        const float StepHeight = 2.5f;
        const float SafeStartLength = 40f;

        // Distances (units from the start) where obstacle groups unlock.
        const float UnlockMedium = 40f / MetersPerUnit;
        const float UnlockWorlds = 70f / MetersPerUnit;
        const float UnlockMoving = 110f / MetersPerUnit;
        const float MetersPerUnit = RunnerSession.MetersPerUnit;

        public float KillY => -12f;
        public float StartX => 0f;

        static readonly Color PastColor = new Color(0.85f, 0.65f, 0.3f);
        static readonly Color FutureColor = new Color(0.3f, 0.8f, 0.95f);
        static readonly Color PastHazardColor = new Color(0.95f, 0.45f, 0.15f);
        static readonly Color FutureHazardColor = new Color(0.75f, 0.3f, 0.95f);

        RunnerShapes _shapes;
        RunnerWorldState _worlds;
        Transform _target;
        System.Random _rng;
        float _cursorX;
        float _groundY;
        Transform _openGround;          // last ground box, extended while the height stays the same
        float _openGroundStartX;
        string _lastPattern;
        readonly List<Transform> _pieces = new List<Transform>();
        readonly List<RunnerWorldMember> _worldSolids = new List<RunnerWorldMember>();

        delegate void Pattern();

        public void Build(RunnerShapes shapes, RunnerWorldState worlds, Transform target, int seed)
        {
            _shapes = shapes;
            _worlds = worlds;
            _target = target;
            Clear();
            _rng = new System.Random(seed);
            _cursorX = StartX - 20f;
            _groundY = 0f;
            _lastPattern = null;
            Flat(SafeStartLength);
        }

        public void Extend(float untilX)
        {
            while (_cursorX < untilX) AddPattern();
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
                    if (member != null) _worldSolids.Remove(member);
                    Destroy(piece.gameObject);
                    _pieces.RemoveAt(i);
                }
            }
        }

        /// <summary>True if a solid piece that only exists in <paramref name="world"/> overlaps the box.
        /// Used to refuse a world switch that would trap the traveler inside a wall.</summary>
        public bool SolidOverlaps(Bounds box, RunnerWorld world)
        {
            foreach (var member in _worldSolids)
            {
                if (member == null || member.World != world) continue;
                var t = member.transform;
                var b = new Bounds(t.position, new Vector3(Mathf.Abs(t.localScale.x), Mathf.Abs(t.localScale.y), 1f));
                if (b.Intersects(box)) return true;
            }
            return false;
        }

        void Clear()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);
            _pieces.Clear();
            _worldSolids.Clear();
            _openGround = null;
        }

        float Distance => _cursorX - StartX;
        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        bool Coin() => _rng.NextDouble() < 0.5;
        RunnerWorld RandomWorld() => Coin() ? RunnerWorld.Past : RunnerWorld.Future;

        void AddPattern()
        {
            // Weighted list of what is unlocked at this distance. Newest group gets extra weight.
            var options = new List<(string name, Pattern build, float weight)>
            {
                ("flat", () => Flat(Range(8f, 12f)), 1.2f),
                ("rock", Rock, 2f),
                ("gap", () => Gap(Range(3f, 4.5f)), 1.6f),
                ("log", Log, 1.6f),
            };
            if (Distance > UnlockMedium)
            {
                options.Add(("stepup", StepUp, 1f));
                options.Add(("stepdown", StepDown, 1f));
                options.Add(("ledge", Ledge, 1f));
                options.Add(("tallwall", TallWall, 1.2f));
            }
            if (Distance > UnlockWorlds)
            {
                options.Add(("worldbridge", WorldBridge, 1.6f));
                options.Add(("worldwall", WorldWall, 1.6f));
                options.Add(("worldsplit", WorldSplit, 1.2f));
            }
            if (Distance > UnlockMoving)
            {
                options.Add(("blade", Blade, 1.3f));
                options.Add(("flyer", Flyer, 1.3f));
                options.Add(("boulders", Boulders, 1.1f));
                options.Add(("crusher", Crusher, 1.1f));
            }

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
                Flat(Range(3f, 6f)); // breathing room between patterns
                return;
            }
        }

        // ---------- basic patterns ----------

        void Flat(float length) => Ground(Mathf.Max(3f, length));

        void Gap(float width)
        {
            Flat(5f);
            _cursorX += width;
            _openGround = null;
            Flat(7f);
        }

        void Rock()
        {
            Flat(5f);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f);
            Flat(8f);
        }

        void Log()
        {
            // Overhead log: standing (3 units) hits it, sliding (1.4 units) passes under.
            Flat(5f);
            Hazard("TrackLog", _cursorX, _groundY + 1.8f, 2.5f, 14f);
            Flat(8f);
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
            Hazard("TrackRock", startX + 9f, _groundY, 1.5f, 1.5f);
        }

        void TallWall()
        {
            // Higher than a single jump (6.5): needs the double jump.
            Flat(6f);
            Hazard("TrackTallWall", _cursorX, _groundY, 1.5f, 7.5f);
            Flat(10f);
        }

        // ---------- world patterns ----------

        void WorldBridge()
        {
            // A gap too wide to jump; a bridge spans it in only one timeline.
            var world = RandomWorld();
            Flat(6f);
            var width = Range(13f, 16f);
            WorldSolid("TrackBridge", world, _cursorX, _groundY - 1f, width, 1f);
            _cursorX += width;
            _openGround = null;
            Flat(8f);
        }

        void WorldWall()
        {
            // A wall too tall to jump that exists in only one timeline: be in the other one.
            var world = RandomWorld();
            Flat(9f);
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
            WorldHazard("TrackSplitWall", wallWorld, _cursorX, _groundY, 1.5f, 16f);
            WorldHazard("TrackSplitLog", logWorld, _cursorX - 0.5f, _groundY + 1.8f, 2.5f, 14f);
            Flat(9f);
        }

        // ---------- moving patterns ----------

        void Blade()
        {
            // Bobs between the ground and above jump height: pass under it or over it.
            Flat(8f);
            var blade = Hazard("TrackBlade", _cursorX, _groundY + 0.2f, 1.5f, 1.5f);
            AddMover(blade, RunnerMover.Mode.Bob, low: _groundY + 0.95f, high: _groundY + 8f, period: 1.8f);
            Flat(10f);
        }

        void Flyer()
        {
            // Flies at head height toward the traveler: slide under it (or jump over early).
            Flat(20f);
            var flyer = Hazard("TrackFlyer", _cursorX, _groundY + 2.1f, 2.2f, 1.2f);
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
            for (var i = 0; i < count; i++)
            {
                var boulder = Hazard("TrackBoulder", _cursorX, _groundY + 18f, 1.8f, 1.8f);
                var mover = AddMover(boulder, RunnerMover.Mode.Drop, low: _groundY + 0.9f, high: 0f, period: 1f);
                mover.triggerDistance = 9f;
                Flat(6f);
            }
            Flat(4f);
        }

        void Crusher()
        {
            // Slams down to the ground and slowly rises: run under while it is up.
            Flat(8f);
            var crusher = Hazard("TrackCrusher", _cursorX, _groundY, 3f, 8f);
            AddMover(crusher, RunnerMover.Mode.Crush, low: _groundY + 4f, high: _groundY + 13f, period: 2.2f,
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
                    RunnerShapes.GroundColor);
                _openGround = box.transform;
                _openGroundStartX = _cursorX;
                _pieces.Add(box.transform);
            }
            _cursorX += length;
        }

        GameObject Hazard(string name, float x, float y, float width, float height)
        {
            var go = _shapes.Box(transform, name, x, y, width, height, RunnerShapes.HazardColor, isTrigger: true,
                sortingOrder: 1);
            go.AddComponent<RunnerHazard>();
            _pieces.Add(go.transform);
            return go;
        }

        void OneWayPlatform(float x, float y, float width, Color color)
        {
            var go = _shapes.Box(transform, "TrackLedge", x, y, width, 1f, color);
            // Solid only from above: no deaths from bumping its side or underside.
            go.GetComponent<BoxCollider2D>().usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            _pieces.Add(go.transform);
        }

        void WorldSolid(string name, RunnerWorld world, float x, float y, float width, float height)
        {
            var color = world == RunnerWorld.Past ? PastColor : FutureColor;
            var go = _shapes.Box(transform, name, x, y, width, height, color);
            var member = go.AddComponent<RunnerWorldMember>();
            member.Init(world, _worlds);
            _worldSolids.Add(member);
            _pieces.Add(go.transform);
        }

        void WorldHazard(string name, RunnerWorld world, float x, float y, float width, float height)
        {
            var color = world == RunnerWorld.Past ? PastHazardColor : FutureHazardColor;
            var go = _shapes.Box(transform, name, x, y, width, height, color, isTrigger: true, sortingOrder: 1);
            go.AddComponent<RunnerHazard>();
            go.AddComponent<RunnerWorldMember>().Init(world, _worlds);
            _pieces.Add(go.transform);
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
