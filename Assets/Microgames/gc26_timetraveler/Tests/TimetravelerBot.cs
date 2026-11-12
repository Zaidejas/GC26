using UnityEngine;

namespace GC26.Timetraveler.Tests
{
    /// <summary>
    /// A simple autopilot for the runner tests. It reads the live track (hazards, ground, world pieces)
    /// and presses the same "buttons" a player would: jump, double jump, slide, switch world.
    /// If the bot cannot get through a level, either the level is unfair or a mechanic is broken.
    /// Jump timing is computed from the controller's real jump curve, not guessed.
    /// </summary>
    internal class TimetravelerBot
    {
        const float LookAhead = 16f;

        readonly RunnerSession _session;
        readonly ContactFilter2D _solidOnly = new ContactFilter2D { useTriggers = false };
        readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        bool _wantDouble;
        float _jumpCooldown;

        public string LastDecision { get; private set; } = "";

        // Last frames before a death, for the failure message.
        readonly System.Collections.Generic.Queue<string> _trace = new System.Collections.Generic.Queue<string>();
        public string Trace => string.Join("\n", _trace);
        string _seen = "";
        string _jumpReason = "";

        void Record(Vector2 pos, float feet, float d, string what)
        {
            _trace.Enqueue($"t={Time.time:0.00} x={pos.x:0.0} feet={feet:0.00} vy={T.VelocityY:0.0} grounded={T.IsGrounded} " +
                           $"slide={T.IsSliding} v={T.RunSpeed:0.0} | {what} | {_seen}");
            while (_trace.Count > 40) _trace.Dequeue();
        }

        public TimetravelerBot(RunnerSession session) => _session = session;

        TravelerController T => _session.Traveler;

        public TravelerInput.ScriptedFrame Next()
        {
            var f = new TravelerInput.ScriptedFrame();
            if (T == null || T.IsDead) return f;
            _jumpCooldown -= Time.deltaTime;

            var pos = (Vector2)T.transform.position;
            var size = T.BodyBounds.size;
            var realFeet = pos.y - size.y / 2f;
            // Judge obstacles from the ground under the traveler, also while in the air
            // (otherwise a log looks like a low obstacle from the top of a jump).
            var below = GroundHeightAhead(realFeet, pos.x);
            var feet = T.IsGrounded || float.IsNegativeInfinity(below) ? realFeet : realFeet + Mathf.Min(0f, below);
            var front = pos.x + size.x / 2f;
            var back = pos.x - size.x / 2f;
            var v = Mathf.Max(1f, T.RunSpeed);

            // Double jump at the top of the first jump, if a tall obstacle asked for it.
            if (_wantDouble && !T.IsGrounded && T.VelocityY <= 0.5f)
            {
                _wantDouble = false;
                f.Jump = true;
                LastDecision = "double jump";
                return f;
            }

            var needSlide = false;
            var jumpAt = float.PositiveInfinity;   // distance to the obstacle at which to jump
            var nearestJumpObstacle = float.PositiveInfinity;
            var needDouble = false;
            var switchWorld = false;

            foreach (var piece in _session.Track.Pieces)
            {
                if (piece == null) continue;
                var col = piece.GetComponent<Collider2D>();
                if (col == null) continue;
                var b = col.bounds;
                if (b.max.x < back - 0.5f || b.min.x > front + LookAhead) continue;
                var member = piece.GetComponent<RunnerWorldMember>();
                var isHazard = piece.GetComponent<RunnerHazard>() != null;
                var d = b.min.x - front;

                if (member != null && !col.enabled)
                {
                    // A piece of the other world. A bridge over a gap ahead: switch to make it real.
                    if (!isHazard && d < 7f && d > -1f && !GroundAhead(feet, b.min.x + 0.5f)) switchWorld = true;
                    continue;
                }
                if (!col.enabled || !isHazard) continue;

                var bottom = b.min.y - feet;
                var top = b.max.y - feet;
                var mover = piece.GetComponent<RunnerMover>();
                if (d < 6f && d > -3f) _seen = $"{piece.name} d={d:0.0} bottom={bottom:0.0} top={top:0.0}";

                // An unjumpable wall of one world: be in the other world.
                if (member != null && top > 9f && bottom < 1f)
                {
                    if (d < 9f) switchWorld = true;
                    continue;
                }
                // Blade and crusher always leave room for a slide.
                if (mover != null && (mover.mode == RunnerMover.Mode.Bob || mover.mode == RunnerMover.Mode.Crush))
                {
                    if (d < 3.2f) needSlide = true;
                    continue;
                }
                if (bottom >= T.tuning.standSize.y + 0.05f) continue;   // above the head
                if (bottom >= T.tuning.slideSize.y + 0.05f)            // head height: slide under
                {
                    if (d < 3.2f) needSlide = true;
                    continue;
                }
                if (top <= 0.05f) continue;                         // below the feet
                if (d < -0.2f) continue;                            // already passing it

                // On the ground: jump. Compute the best take-off distance for this height and width.
                if (d < nearestJumpObstacle)
                {
                    nearestJumpObstacle = d;
                    var width = b.size.x + size.x;
                    if (BestTakeOff(top, width, v, out var takeOff)) { jumpAt = takeOff; needDouble = false; }
                    else { jumpAt = v * 0.2f; needDouble = true; }
                    _jumpReason = $"{piece.name} d={d:0.0} top={top:0.0}";
                }
            }

            // Gaps and steps in the ground (solid only).
            var gapAt = float.PositiveInfinity;
            for (var x = 0.2f; x < 6f; x += 0.3f)
            {
                if (!GroundAhead(feet, front + x)) { gapAt = x; break; }
                var h = GroundHeightAhead(feet, front + x);
                if (h > 1.1f) { if (x < nearestJumpObstacle) { nearestJumpObstacle = x; jumpAt = Mathf.Max(0.6f, v * 0.12f); needDouble = false; _jumpReason = $"step h={h:0.0} at +{x:0.0}"; } break; }
            }

            Record(pos, feet, nearestJumpObstacle, $"jumpObst={nearestJumpObstacle:0.0} jumpAt={jumpAt:0.0} gapAt={gapAt:0.0} slide={needSlide} switch={switchWorld}");
            _seen = "";

            if (switchWorld && _session.Worlds.CanSwitch)
            {
                f.Switch = true;
                LastDecision = "switch world";
                return f;
            }

            if (needSlide)
            {
                f.SlideHeld = true;
                f.Slide = !T.IsSliding;
                LastDecision = "slide";
                return f;
            }

            if (T.IsGrounded && _jumpCooldown <= 0f)
            {
                if (gapAt <= 0.5f && !switchWorld)
                {
                    f.Jump = true;
                    _jumpCooldown = 0.2f;
                    LastDecision = $"jump gap at x={pos.x:0.0}";
                    _trace.Enqueue(">>> " + LastDecision);
                }
                else if (!float.IsInfinity(nearestJumpObstacle) && nearestJumpObstacle <= jumpAt)
                {
                    f.Jump = true;
                    _wantDouble = needDouble;
                    _jumpCooldown = 0.2f;
                    LastDecision = (needDouble ? "jump (double) for " : "jump for ") + _jumpReason + $" at x={pos.x:0.0}";
                    _trace.Enqueue(">>> " + LastDecision);
                }
            }
            return f;
        }

        bool GroundAhead(float feet, float x) => !float.IsNegativeInfinity(GroundHeightAhead(feet, x));

        /// <summary>Height of solid ground at x relative to the feet (-inf if none within reach below).</summary>
        float GroundHeightAhead(float feet, float x)
        {
            var origin = new Vector2(x, feet + 3f);
            var n = Physics2D.Raycast(origin, Vector2.down, _solidOnly, _hits, 20f);
            for (var i = 0; i < n; i++)
                if (_hits[i].distance > 0f && _hits[i].collider.GetComponent<TravelerController>() == null)
                    return _hits[i].point.y - feet;
            return float.NegativeInfinity;
        }

        // Jump curve of TravelerController (default tuning): rise with g, fall with g * 1.2.
        static readonly float G = 2f * 6.5f / (0.42f * 0.42f);
        static readonly float JumpSpeed = G * 0.42f;

        /// <summary>
        /// Take-off distance (to the obstacle's left edge) that centres the jump over an obstacle of the given
        /// height and width (traveler width included). False if a single jump cannot clear it.
        /// </summary>
        static bool BestTakeOff(float height, float width, float speed, out float distance)
        {
            distance = 0f;
            const float apexT = 0.42f;
            const float apexY = 6.5f;
            if (height >= apexY - 0.1f) return false;
            // Rising: y = J t - G t^2 / 2 = height
            var disc = JumpSpeed * JumpSpeed - 2f * G * height;
            var t1 = (JumpSpeed - Mathf.Sqrt(disc)) / G;
            // Falling from the apex with 1.2 G.
            var t2 = apexT + Mathf.Sqrt(2f * (apexY - height) / (G * 1.2f));
            var slack = (t2 - t1) - width / speed;
            if (slack < 0.06f) return false;
            distance = speed * (t1 + slack * 0.5f);
            return true;
        }
    }
}
