using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The T-Rex boss at the end of 1-5, in two acts.
    /// CHASE: from the checkpoint on, the T-Rex runs right behind the traveler (its jaws at the left edge of the
    /// screen) while she dodges quick obstacles. ARENA: she reaches a cliff, stops, turns round, and a rhythm fight
    /// starts: plain squares pop up around the screen and fill up from the middle; tap a square the moment it is
    /// full, on the beat of a generated drum loop. HOLD bars: tap the start on the beat and keep the finger down
    /// until the bar has filled. Hits strike the T-Rex; a miss (or letting go of a bar too soon) lets it bite.
    /// The fight clock follows the music actually playing, so what you see matches what you hear.
    /// Bring its health to zero before it takes all your hearts. Losing restarts only the fight.
    /// Mouse: click, or Z / X / Space with the cursor on the square.
    /// Art slot: boss_trex (whole T-Rex facing right, feet at the bottom); until then a box-built T-Rex.
    /// </summary>
    public class RunnerBoss : MonoBehaviour
    {
        public enum State { Waiting, Chase, Intro, Fight, Won, Lost }
        public enum NoteKind { Circle, Slider }

        public class Note
        {
            public float Time;
            public float EndTime;           // sliders: when the ball reaches the end
            public NoteKind Kind;
            public Vector2 Pos;             // 0..1 inside the playfield
            public Vector2 EndPos;          // sliders
            public bool Judged;
            public bool Holding;            // slider pressed, being followed
            public bool PerfectStart;
        }

        const float Bpm = 120f;
        const float Step = 60f / Bpm / 2f;     // one eighth note = 0.25 s
        const float LeadIn = 2f;               // a 4-beat count-in before the first note
        const float HitWindow = 0.15f;
        const float PerfectWindow = 0.06f;
        const float Approach = 1.1f;           // seconds from a circle appearing to "tap now"
        const float HoldReleaseGrace = 0.12f;  // HOLD bars: letting go this early still counts
        const float IntroTime = 2.2f;
        public const int MaxHealth = 22;
        public const int MaxHearts = 6;

        // The chart, one bar (8 eighth notes) per string: o circle, L slider (each - after it = one more step long),
        // . rest. It gets busier as the fight goes on; the last four bars repeat until somebody wins.
        static readonly string[] Chart =
        {
            "o...o...", "o...o...", "o...L-..", "o...o...",
            "o.o.o...", "L---o...", "o...o.o.", "L-..L-..",
            "o.o.o.o.", "L---L---", "o.o.L-..", "o.o.o.o.",
        };
        const int LoopFrom = 8, LoopTimes = 3;

        static readonly Color BossColor = new Color(0.27f, 0.47f, 0.27f);
        static readonly Color BossDark = new Color(0.18f, 0.33f, 0.19f);
        static readonly Color TapColor = new Color(1f, 0.82f, 0.25f);
        static readonly Color NoteColor = new Color(1f, 0.82f, 0.3f);

        public State CurrentState { get; private set; } = State.Waiting;
        public int Health { get; private set; } = MaxHealth;
        public int Hearts { get; private set; } = MaxHearts;
        /// <summary>Song clock of the fight in seconds (0 = first bar).</summary>
        public float SongTime { get; private set; }
        /// <summary>The fight has taken over the run (no running, no normal input).</summary>
        public bool InControl => CurrentState >= State.Intro;
        /// <summary>Where the camera should frame the traveler (0 left .. 1 right), or null for the normal framing.</summary>
        public float? CameraAnchor => CurrentState == State.Chase ? 0.34f : InControl ? 0.78f : (float?)null;
        public event Action Defeated;

        RunnerShapes _shapes;
        RunnerFx _fx;
        RunnerCamera _follow;
        TravelerController _traveler;
        TravelerAnimator _anim;
        TravelerInput _input;
        RunnerTrack _track;
        TimetravelerSettings _settings;
        /// <summary>The song time the player hears (settings: rhythm offset for phones whose sound comes late).</summary>
        float HeardTime => SongTime - _settings.RhythmOffsetMs / 1000f;

        Transform _root;
        readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>();
        readonly List<Color> _partColors = new List<Color>();
        Transform _jaw, _legA, _legB;
        float _jawRestY;
        float _x, _y;                 // boss feet position
        float _stateTime;
        float _recoil;                // pushed back when hit (units)
        float _lunge;                 // lunges forward when it bites
        float _flash;
        float _stompTimer;
        float _roar;

        readonly List<Note> _notes = new List<Note>();
        string _judgement = "";
        Color _judgementColor;
        float _judgementTime;
        int _combo;

        AudioSource _music;
        AudioSource _sfx;
        AudioClip _song, _hitClip, _missClip;

        // The music follows the game clock: pause (time scale 0) stops it, the tests' speed-up speeds it up.
        void Update()
        {
            if (_music != null)
            {
                _music.pitch = Time.timeScale;
                _music.volume = _settings != null ? _settings.MusicVolume : 0.8f;
            }
        }

        // ---------- set-up ----------

        public void Init(RunnerShapes shapes, RunnerFx fx, RunnerCamera follow, TravelerController traveler,
            TravelerAnimator anim, TravelerInput input, RunnerTrack track, TimetravelerSettings settings = null)
        {
            _settings = settings ?? TimetravelerSettings.Defaults();
            _shapes = shapes;
            _fx = fx;
            _follow = follow;
            _traveler = traveler;
            _anim = anim;
            _input = input;
            _track = track;
            BuildBody();
            BuildChart();
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.volume = _settings.MusicVolume;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _song = BuildSong();

            _hitClip = Blip(1320f, 0.07f, 0.35f);
            _missClip = Blip(140f, 0.18f, 0.5f);
            ResetForRun();
        }

        /// <summary>A new attempt of the level (start or checkpoint): the boss waits for the chase again.</summary>
        public void ResetForRun()
        {
            CurrentState = State.Waiting;
            _stateTime = 0f;
            Health = MaxHealth;
            Hearts = MaxHearts;
            if (_music != null) _music.Stop();
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                _root.localRotation = Quaternion.identity;
            }
            if (_anim != null) _anim.FaceLeft = false;
        }

        void BuildBody()
        {
            _root = new GameObject("BossTRex").transform;
            _root.SetParent(transform, false);
            var art = TimetravelerArt.Sprite(TimetravelerArt.Boss);
            if (art != null)
            {
                var go = new GameObject("Art");
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art;
                sr.sharedMaterial = _shapes.Material;
                sr.sortingOrder = 4;
                go.transform.localPosition = new Vector3(-art.bounds.center.x, art.bounds.extents.y - art.bounds.center.y, 0f);
                AddPart(sr);
                return;
            }
            // Box-built T-Rex facing right, feet at (0, 0): tail, body, neck, head, jaw, teeth, eye, arm, legs.
            Part("Tail", new Vector2(4.2f, 1.3f), new Vector2(-4.4f, 4f), BossDark);
            _legA = Part("LegBack", new Vector2(1.2f, 2.6f), new Vector2(-1.9f, 1.3f), BossDark).transform;
            Part("Body", new Vector2(5f, 3.4f), new Vector2(-0.8f, 4f), BossColor);
            _legB = Part("LegFront", new Vector2(1.2f, 2.6f), new Vector2(0.3f, 1.3f), BossColor).transform;
            Part("Neck", new Vector2(1.8f, 2.2f), new Vector2(1.5f, 5.8f), BossColor);
            Part("Head", new Vector2(3.4f, 1.9f), new Vector2(2.8f, 7.3f), BossColor);
            Part("Teeth", new Vector2(2.4f, 0.3f), new Vector2(3.1f, 6.25f), new Color(0.95f, 0.95f, 0.88f));
            _jaw = Part("Jaw", new Vector2(2.8f, 0.7f), new Vector2(2.9f, 5.95f), BossDark).transform;
            _jawRestY = _jaw.localPosition.y;
            Part("Eye", new Vector2(0.45f, 0.45f), new Vector2(3.7f, 7.7f), new Color(1f, 0.85f, 0.2f));
            Part("Arm", new Vector2(1f, 0.45f), new Vector2(2.1f, 4.5f), BossDark);
        }

        SpriteRenderer Part(string name, Vector2 size, Vector2 at, Color color)
        {
            var sr = _shapes.Visual(_root, name, size, color, 4);
            sr.transform.localPosition = at;
            AddPart(sr);
            return sr;
        }

        void AddPart(SpriteRenderer sr)
        {
            _parts.Add(sr);
            _partColors.Add(sr.color);
        }

        /// <summary>
        /// Times from the chart. Positions: the 9 points of a 3 x 3 grid over the whole screen; each box lands at
        /// least one grid step away from the last one (never on top of it). Bars run right across the screen.
        /// </summary>
        void BuildChart()
        {
            _notes.Clear();
            var bars = new List<string>(Chart);
            for (var i = 0; i < LoopTimes; i++)
                for (var b = LoopFrom; b < Chart.Length; b++) bars.Add(Chart[b]);
            var rng = new System.Random(26);
            var col = 1;
            var row = 1;
            for (var bar = 0; bar < bars.Count; bar++)
                for (var s = 0; s < 8; s++)
                {
                    var c = bars[bar][s];
                    if (c != 'o' && c != 'L') continue;
                    var steps = 1;
                    while (s + steps < 8 && bars[bar][s + steps] == '-') steps++;
                    if (_notes.Count > 0)
                    {
                        // Another grid point, not next to the last one if it can be helped.
                        int nc, nr;
                        var tries = 0;
                        do
                        {
                            nc = rng.Next(3);
                            nr = rng.Next(3);
                            tries++;
                        } while ((Mathf.Abs(nc - col) + Mathf.Abs(nr - row) < 2) && tries < 20);
                        col = nc;
                        row = nr;
                    }
                    var time = (bar * 8 + s) * Step;
                    var note = new Note
                    {
                        Time = time,
                        EndTime = time,
                        Kind = c == 'L' ? NoteKind.Slider : NoteKind.Circle,
                        Pos = Grid(col, row),
                    };
                    note.EndPos = note.Pos;
                    if (note.Kind == NoteKind.Slider)
                    {
                        // Right across the screen: from this edge to the other one, sideways or up/down.
                        note.EndTime = time + steps * Step;
                        var across = rng.NextDouble() < 0.6;
                        if (across)
                        {
                            if (col == 1) col = 0;
                            note.Pos = Grid(col, row);
                            col = 2 - col;
                        }
                        else
                        {
                            if (row == 1) row = 0;
                            note.Pos = Grid(col, row);
                            row = 2 - row;
                        }
                        note.EndPos = Grid(col, row);
                    }
                    _notes.Add(note);
                }
        }

        static Vector2 Grid(int col, int row) => new Vector2(0.5f * col, 0.5f * row);

        // ---------- per frame (called by the session) ----------

        public void Tick()
        {
            if (_traveler == null) return;
            var dt = Time.deltaTime;
            _stateTime += dt;
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 4f);
            _lunge = Mathf.MoveTowards(_lunge, 0f, dt * 6f);
            _flash -= dt;
            _judgementTime -= dt;
            _roar -= dt;

            var tx = _traveler.transform.position.x;
            switch (CurrentState)
            {
                case State.Waiting:
                    if (tx >= _track.ChaseStartX && !_traveler.IsDead) StartChase();
                    break;
                case State.Chase:
                    if (_traveler.IsDead) break;
                    // Right behind her: the jaws (front of the head, 4.4 units from the feet) about 1.4 units behind.
                    // (It charges in from off screen first.)
                    _x = Mathf.Lerp(_x, tx - 5.8f + Mathf.Sin(_stateTime * 2.6f) * 0.7f, 1f - Mathf.Exp(-3f * dt));
                    if (_traveler.IsGrounded) _y = Mathf.Lerp(_y, Feet.y, 1f - Mathf.Exp(-8f * dt));
                    Stomp(dt);
                    if (tx >= _track.ArenaX && _traveler.IsGrounded) StartIntro();
                    break;
                case State.Intro:
                    // The T-Rex steps back and roars; she turns round to face it.
                    _x = Mathf.Lerp(_x, tx - 9f, 1f - Mathf.Exp(-3f * dt));
                    if (_stateTime >= IntroTime) StartFight();
                    break;
                case State.Fight:
                    SongTime += dt;
                    SyncToMusic();
                    Judge();
                    if (Health <= 0) Win();
                    else if (Hearts <= 0) Lose();
                    break;
                case State.Won:
                    _root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 80f, Mathf.Clamp01(_stateTime / 0.8f)));
                    if (_stateTime > 0.4f && _stateTime - dt <= 0.4f)
                        _fx.Burst(new Vector2(_x, _y + 1f), 30, new Color(0.8f, 0.7f, 0.5f), new Vector2(0f, 5f), 10f, 0.5f, 0.9f);
                    if (_stateTime >= 2f && _stateTime - dt < 2f) Defeated?.Invoke();
                    break;
                case State.Lost:
                    if (_stateTime > 1f && _input.AnyPressed) StartFight();
                    break;
            }
            Animate();
        }

        /// <summary>The traveler died during the chase: the T-Rex closes its jaws on the spot.</summary>
        public void OnTravelerDied()
        {
            if (CurrentState != State.Chase) return;
            _lunge = 2f;
            _roar = 1.2f;
        }

        Vector2 Feet => (Vector2)_traveler.transform.position - new Vector2(0f, _traveler.ColliderHeight / 2f);

        void StartChase()
        {
            CurrentState = State.Chase;
            _stateTime = 0f;
            _x = _traveler.transform.position.x - 12f;
            _y = Feet.y;
            _roar = 1.6f;
            _root.gameObject.SetActive(true);
            _root.localRotation = Quaternion.identity;
            _follow.Shake(0.35f, 0.4f);
            TimetravelerLog.Info("Boss chase started.", this);
        }

        void StartIntro()
        {
            CurrentState = State.Intro;
            _stateTime = 0f;
            _traveler.Halt();
            if (_anim != null) _anim.FaceLeft = true;
            _roar = IntroTime;
            _follow.Shake(0.4f, 0.5f);
            TimetravelerLog.Info("Boss fight: the traveler is cornered.", this);
        }

        void StartFight()
        {
            CurrentState = State.Fight;
            _stateTime = 0f;
            Health = MaxHealth;
            Hearts = MaxHearts;
            _combo = 0;
            _judgement = "";
            SongTime = -LeadIn;
            foreach (var n in _notes)
            {
                n.Judged = false;
                n.Holding = false;
            }
            _effects.Clear();
            _pointerWasDown = true;   // a finger still down from the intro does not count as a tap
            _root.localRotation = Quaternion.identity;
            if (_anim != null) _anim.PlayPose(TravelerAnimator.Pose.Attack, 0f);
            if (_music != null && _song != null)
            {
                _music.clip = _song;
                _music.time = 0f;
                _music.Play();
            }
            TimetravelerLog.Info("Boss fight started.", this);
        }

        void Win()
        {
            CurrentState = State.Won;
            _stateTime = 0f;
            _roar = 0f;
            if (_music != null) _music.Stop();
            _follow.Shake(0.5f, 0.6f);
            Say("T-REX DEFEATED!", TapColor, 3f);
            TimetravelerLog.Info($"Boss defeated ({Hearts} hearts left).", this);
        }

        void Lose()
        {
            CurrentState = State.Lost;
            _stateTime = 0f;
            if (_music != null) _music.Stop();
            if (_anim != null) _anim.PlayPose(TravelerAnimator.Pose.Hurt, 999f);
            TimetravelerLog.Info($"Boss fight lost (boss health {Health}).", this);
        }

        // ---------- the rhythm part ----------

        /// <summary>
        /// Keeps the fight clock on the music that is really playing (the audio runs on its own clock and starts
        /// a little late): small drift is eased out, big drift is snapped. Without audio (tests) the frame clock runs alone.
        /// </summary>
        void SyncToMusic()
        {
            if (_music == null || !_music.isPlaying || _music.clip != _song || _song == null) return;
            var heard = _music.timeSamples / (float)_song.frequency - LeadIn;
            var drift = heard - SongTime;
            if (Mathf.Abs(drift) > 0.08f) SongTime = heard;
            else SongTime += drift * 0.15f;
        }

        /// <summary>Tests: a stand-in for the finger (down, position in GUI pixels). Null = the real pointer.</summary>
        internal Func<(bool down, Vector2 pos)> ScriptedPointer;
        bool _pointerWasDown;
        readonly List<(Vector2 pos, float time, Color color, string text)> _effects = new List<(Vector2, float, Color, string)>();

        /// <summary>Finger / mouse (or Z, X, Space with the mouse) this frame: held, newly pressed, where.</summary>
        void ReadPointer(out bool down, out bool pressed, out Vector2 pos)
        {
            if (ScriptedPointer != null)
            {
                var f = ScriptedPointer();
                down = f.down;
                pos = f.pos;
                pressed = down && !_pointerWasDown;
                _pointerWasDown = down;
                return;
            }
            down = pressed = false;
            pos = default;
            var pointer = Pointer.current;
            if (pointer != null)
            {
                var raw = pointer.position.ReadValue();
                pos = TimetravelerUi.ToGui(raw);
                down = pointer.press.isPressed;
                pressed = pointer.press.wasPressedThisFrame && !_input.IgnoreRect.Contains(raw);   // not the pause button
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                down |= kb.zKey.isPressed || kb.xKey.isPressed || kb.spaceKey.isPressed;
                pressed |= kb.zKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            }
        }

        void Judge()
        {
            ReadPointer(out var down, out var pressed, out var pos);
            var now = HeardTime;
            var radius = CircleRadius;

            if (pressed)
            {
                // The earliest open note in its window that the tap actually lands on.
                foreach (var n in _notes)
                {
                    if (n.Judged || n.Holding) continue;
                    if (n.Time - now > HitWindow) break;
                    if (Mathf.Abs(n.Time - now) > HitWindow) continue;
                    var d = pos - ToScreen(n.Pos);
                    if (Mathf.Abs(d.x) > radius * 1.3f || Mathf.Abs(d.y) > radius * 1.3f) continue;
                    var perfect = Mathf.Abs(n.Time - now) <= PerfectWindow;
                    if (n.Kind == NoteKind.Circle)
                    {
                        n.Judged = true;
                        Hit(n, perfect, ToScreen(n.Pos));
                    }
                    else
                    {
                        n.Holding = true;
                        n.PerfectStart = perfect;
                        _effects.Add((ToScreen(n.Pos), 0f, NoteColor, ""));
                        if (_sfx != null && _hitClip != null) _sfx.PlayOneShot(_hitClip, 0.4f * _settings.SoundVolume);
                    }
                    break;
                }
            }

            foreach (var n in _notes)
            {
                if (n.Time - now > HitWindow) break;
                if (n.Judged) continue;
                if (n.Holding)
                {
                    // HOLD: just keep the finger (or key) down until the bar is full; no need to move.
                    // Letting go a moment before the end still counts.
                    if (now >= n.EndTime || (!down && now >= n.EndTime - HoldReleaseGrace))
                    {
                        n.Holding = false;
                        n.Judged = true;
                        Hit(n, n.PerfectStart, ToScreen(n.EndPos));
                    }
                    else if (!down)
                    {
                        n.Holding = false;
                        n.Judged = true;
                        Miss("LET GO TOO SOON", ToScreen(SliderBall(n, now)));
                    }
                }
                else if (now - n.Time > HitWindow)
                {
                    n.Judged = true;
                    Miss("MISS", ToScreen(n.Pos));
                }
            }
        }

        void Hit(Note note, bool perfect, Vector2 at)
        {
            Health = Mathf.Max(0, Health - 1);
            _combo++;
            _recoil = perfect ? 1f : 0.6f;
            _flash = 0.1f;
            var color = NoteColor;
            _effects.Add((at, 0f, color, perfect ? "PERFECT" : "GOOD"));
            if (_anim != null)
                _anim.PlayPose(_combo % 2 == 0 ? TravelerAnimator.Pose.Attack : TravelerAnimator.Pose.Dodge, 0.22f);
            // A burst of time energy flies from her to the T-Rex's head.
            var from = (Vector2)_traveler.transform.position;
            var head = HeadPosition;
            _fx.Burst(from, 6, color, (head - from).normalized * 14f, 2f, 0.3f, 0.3f, falls: false);
            _fx.Burst(head, perfect ? 14 : 8, color, Vector2.zero, 7f, 0.35f, 0.4f, falls: false);
            _follow.Shake(perfect ? 0.12f : 0.06f, 0.08f);
            if (_sfx != null && _hitClip != null) _sfx.PlayOneShot(_hitClip, (perfect ? 0.9f : 0.6f) * _settings.SoundVolume);
        }

        void Miss(string text, Vector2 at)
        {
            Hearts = Mathf.Max(0, Hearts - 1);
            _combo = 0;
            _lunge = 1.6f;
            _effects.Add((at, 0f, new Color(1f, 0.35f, 0.3f), "X"));
            Say(text, new Color(1f, 0.4f, 0.35f), 0.5f);
            if (_anim != null) _anim.PlayPose(TravelerAnimator.Pose.Hurt, 0.3f);
            _fx.Burst(_traveler.transform.position, 8, RunnerShapes.HazardColor, new Vector2(3f, 3f), 5f, 0.3f, 0.4f);
            _follow.Shake(0.3f, 0.18f);
            if (_sfx != null && _missClip != null) _sfx.PlayOneShot(_missClip, 0.8f * _settings.SoundVolume);
        }

        void Say(string text, Color color, float seconds)
        {
            _judgement = text;
            _judgementColor = color;
            _judgementTime = seconds;
        }

        /// <summary>Where a slider's ball is at a song time (0..1 playfield coordinates).</summary>
        internal Vector2 SliderBall(Note n, float time)
        {
            if (n.Kind != NoteKind.Slider || n.EndTime <= n.Time) return n.Pos;
            return Vector2.Lerp(n.Pos, n.EndPos, Mathf.Clamp01((time - n.Time) / (n.EndTime - n.Time)));
        }

        // Playfield: nearly the whole screen (under the top bar and the boss health, above the bottom edge).
        // Grid points are the box centres, so the boxes reach almost to the edges.
        Rect Playfield
        {
            get
            {
                var unit = Screen.height / 40f;
                var safe = Screen.safeArea;
                var top = Screen.height - safe.yMax;
                var half = CircleRadius;
                var y0 = top + unit * 9f + half;
                var y1 = Screen.height - safe.y - unit * 2f - half;
                return new Rect(safe.x + unit * 0.6f + half, y0, safe.width - unit * 1.2f - half * 2f, y1 - y0);
            }
        }

        /// <summary>Half the size of a box, in pixels: big enough for a thumb, small enough for 3 across.</summary>
        float CircleRadius
        {
            get
            {
                var unit = Screen.height / 40f;
                return Mathf.Min(unit * 3f, Screen.safeArea.width / 7f);
            }
        }

        /// <summary>Playfield position (0..1) to GUI pixels.</summary>
        internal Vector2 ToScreen(Vector2 p)
        {
            var f = Playfield;
            return new Vector2(f.x + p.x * f.width, f.y + p.y * f.height);
        }

        /// <summary>Dev hotkey (F12): the T-Rex falls now (in the fight).</summary>
        internal void DevWin()
        {
            if (CurrentState == State.Fight || CurrentState == State.Intro) Win();
        }

        /// <summary>Tests: the next note still to be played (or being held), or null.</summary>
        internal Note NextNote()
        {
            foreach (var n in _notes) if (!n.Judged) return n;
            return null;
        }

        /// <summary>Tests: the song time the judging uses (with the player's rhythm offset).</summary>
        internal float JudgeTime => HeardTime;

        // ---------- look ----------

        Vector2 HeadPosition => new Vector2(_x + 3f - _recoil + _lunge, _y + 7f);

        void Stomp(float dt)
        {
            _stompTimer -= dt;
            if (_stompTimer > 0f) return;
            _stompTimer = 0.6f;
            _follow.Shake(0.03f, 0.06f);
            _fx.Burst(new Vector2(_x, _y + 0.1f), 3, new Color(0.78f, 0.68f, 0.52f, 0.9f), new Vector2(-2f, 2f), 1.5f, 0.35f, 0.4f);
        }

        void Animate()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            _root.position = new Vector3(_x - _recoil + _lunge, _y, 0f);
            // Running legs in the chase, jaw chomping when it roars or bites.
            var running = CurrentState == State.Chase;
            var t = Time.time * 9f;
            if (_legA != null) _legA.localPosition = new Vector3(-1.9f + (running ? Mathf.Sin(t) * 0.5f : 0f), 1.3f + (running ? Mathf.Max(0f, Mathf.Sin(t)) * 0.4f : 0f), 0f);
            if (_legB != null) _legB.localPosition = new Vector3(0.3f - (running ? Mathf.Sin(t) * 0.5f : 0f), 1.3f + (running ? Mathf.Max(0f, -Mathf.Sin(t)) * 0.4f : 0f), 0f);
            if (_jaw != null)
            {
                var open = _roar > 0f || _lunge > 0.2f ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 14f)) : 0f;
                _jaw.localPosition = new Vector3(_jaw.localPosition.x, _jawRestY - open * 0.9f, 0f);
            }
            for (var i = 0; i < _parts.Count; i++)
                _parts[i].color = _flash > 0f ? Color.white : _partColors[i];
        }

        // ---------- screen ----------

        GUIStyle _style, _small;

        void OnGUI()
        {
            if (_traveler == null || CurrentState == State.Waiting) return;
            var unit = Screen.height / 40f;
            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _small = new GUIStyle(_style);
            }
            _style.fontSize = Mathf.RoundToInt(unit * 1.4f);
            _small.fontSize = Mathf.RoundToInt(unit * 0.75f);
            var style = _style;
            var small = _small;

            if (_roar > 0f && CurrentState <= State.Intro)
                Label(new Rect(0, Screen.height * 0.2f, Screen.width, unit * 2.5f), "ROAAAR!", style, new Color(1f, 0.5f, 0.3f));
            if (CurrentState == State.Chase) return;

            // Boss health and the traveler's hearts, under the top bar.
            var y = top + unit * 5.2f;
            var barRect = new Rect(safe.x + unit * 4.5f, y, safe.width - unit * 5.5f, unit * 1.1f);
            Label(new Rect(safe.x + unit * 0.5f, y - unit * 0.2f, unit * 4f, unit * 1.5f), "T-REX", small, Color.white);
            Fill(barRect, new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(barRect.x, barRect.y, barRect.width * Health / MaxHealth, barRect.height), new Color(0.85f, 0.25f, 0.2f));
            for (var i = 0; i < MaxHearts; i++)
                Fill(new Rect(safe.x + unit * (0.6f + i * 1.3f), y + unit * 1.6f, unit, unit),
                    i < Hearts ? new Color(1f, 0.35f, 0.45f) : new Color(0.3f, 0.3f, 0.3f, 0.7f));
            if (_combo >= 4)
                Label(new Rect(safe.xMax - unit * 8f, y + unit * 1.3f, unit * 7.5f, unit * 1.5f), $"COMBO x{_combo}", small, TapColor);

            if (CurrentState == State.Intro)
            {
                Label(new Rect(0, Screen.height * 0.3f, Screen.width, unit * 2.5f), "FIGHT!", style, TapColor);
                var touch = Touchscreen.current != null || Application.isMobilePlatform;
                Label(new Rect(safe.x, Screen.height * 0.3f + unit * 2.5f, safe.width, unit * 3f),
                    touch ? "Tap the box when it is full.\nHOLD: keep your finger down until the bar fills."
                          : "Click the box when it is full (or Z / X).\nHOLD: keep it pressed until the bar fills.",
                    small, Color.white);
            }

            if (CurrentState == State.Fight) DrawPlayfield(unit, small);

            if (_judgementTime > 0f && !string.IsNullOrEmpty(_judgement))
                Label(new Rect(0, Screen.height * 0.36f, Screen.width, unit * 2.5f), _judgement, style,
                    new Color(_judgementColor.r, _judgementColor.g, _judgementColor.b, Mathf.Clamp01(_judgementTime * 3f)));

            if (CurrentState == State.Lost)
            {
                Label(new Rect(0, Screen.height * 0.3f, Screen.width, unit * 2.5f), "The T-Rex won...", style, new Color(1f, 0.4f, 0.35f));
                if (_stateTime > 1f)
                    Label(new Rect(0, Screen.height * 0.3f + unit * 2.6f, Screen.width, unit * 2f), "Tap to fight again", style, Color.white);
            }
        }

        /// <summary>
        /// Only two boxes at a time: the one to hit now (big, bright, "TAP" / "HOLD") and a faint outline of the next,
        /// linked by a thin line. The scene behind is dimmed so they stand out.
        /// </summary>
        void DrawPlayfield(float unit, GUIStyle small)
        {
            var now = HeardTime;
            var r = CircleRadius;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));

            Note current = null, next = null;
            foreach (var n in _notes)
            {
                if (n.Judged) continue;
                if (current == null) current = n;
                else { next = n; break; }
            }

            if (next != null && current != null && next.Time - now < Approach + 0.6f)
            {
                var a = ToScreen(current.Kind == NoteKind.Slider ? current.EndPos : current.Pos);
                var b = ToScreen(next.Pos);
                Line(a, b, new Color(1f, 1f, 1f, 0.25f));
                DrawBox(next, now, r * 0.75f, false, small);
            }
            if (current != null && current.Time - now < Approach + 0.6f) DrawBox(current, now, r, true, small);

            // Hit marks: a growing frame and the grade, fading out.
            for (var i = _effects.Count - 1; i >= 0; i--)
            {
                var fx = _effects[i];
                fx.time += Time.deltaTime;
                if (fx.time > 0.4f) { _effects.RemoveAt(i); continue; }
                _effects[i] = fx;
                var alpha = 1f - fx.time / 0.4f;
                var col = new Color(fx.color.r, fx.color.g, fx.color.b, alpha);
                Frame(Square(fx.pos, r * (1f + fx.time * 1.5f)), col, 4f);
                if (!string.IsNullOrEmpty(fx.text))
                    Label(new Rect(fx.pos.x - r * 2f, fx.pos.y - r * 1.9f, r * 4f, r), fx.text, _style, col);
            }
        }

        void DrawBox(Note n, float now, float r, bool active, GUIStyle small)
        {
            var c = ToScreen(n.Pos);
            var white = new Color(1f, 1f, 1f, active ? 1f : 0.45f);
            if (n.Kind == NoteKind.Slider)
            {
                var e = ToScreen(n.EndPos);
                var bar = Rect.MinMaxRect(Mathf.Min(c.x, e.x) - r, Mathf.Min(c.y, e.y) - r, Mathf.Max(c.x, e.x) + r, Mathf.Max(c.y, e.y) + r);
                Fill(bar, new Color(0f, 0f, 0f, active ? 0.55f : 0.25f));
                Frame(bar, white, active ? 4f : 2f);
                Line(c, e, new Color(NoteColor.r, NoteColor.g, NoteColor.b, active ? 0.8f : 0.3f), 6f);
                if (active && n.Holding)
                {
                    // The bar fills up from the start while it is held.
                    var tip = ToScreen(SliderBall(n, now));
                    var done = Rect.MinMaxRect(Mathf.Min(c.x, tip.x) - r, Mathf.Min(c.y, tip.y) - r, Mathf.Max(c.x, tip.x) + r, Mathf.Max(c.y, tip.y) + r);
                    Fill(done, NoteColor);
                    Frame(bar, Color.white, 4f);
                    Label(Square(c, r), "HOLD", _style, Color.black);
                    return;
                }
            }
            if (!active)
            {
                Frame(Square(c, r), white, 2f);
                Label(Square(c, r), "next", small, white);
                return;
            }
            // Fills from the middle; full = now.
            var fill = 1f - Mathf.Clamp01((n.Time - now) / Approach);
            Fill(Square(c, r), new Color(0f, 0f, 0f, 0.7f));
            Fill(Square(c, r * fill), NoteColor);
            Frame(Square(c, r), Color.white, 5f);
            Label(Square(c, r), n.Kind == NoteKind.Slider ? "HOLD" : "TAP", _style, fill > 0.6f ? Color.black : Color.white);
        }

        static void Line(Vector2 a, Vector2 b, Color color, float width = 3f)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 6f));
            for (var i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                Fill(new Rect(p.x - width / 2f, p.y - width / 2f, width, width), color);
            }
        }

        static Rect Square(Vector2 centre, float half) => new Rect(centre.x - half, centre.y - half, half * 2f, half * 2f);

        static void Frame(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * color.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        // ---------- generated music (no audio files needed) ----------

        const int Rate = 22050;

        /// <summary>Count-in, then drums and a bass line at 120 BPM, with a soft pluck on every note of the chart.</summary>
        AudioClip BuildSong()
        {
            var lastNote = _notes.Count > 0 ? _notes[_notes.Count - 1].Time : 0f;
            var length = LeadIn + lastNote + 2f;
            var data = new float[Mathf.CeilToInt(length * Rate)];
            var rng = new System.Random(5);
            float[] roots = { 110f, 87.31f, 130.81f, 98f };   // A, F, C, G

            for (var i = 0; i < 4; i++) Click(data, i * Step * 2f, 1760f, 0.05f, 0.35f);   // count-in
            var steps = Mathf.FloorToInt((lastNote + 2f) / Step);
            for (var s = 0; s < steps; s++)
            {
                var t = LeadIn + s * Step;
                if (s % 2 == 0) Kick(data, t);
                if (s % 4 == 2) Noise(data, t, 0.12f, 0.35f, rng);   // snare on 2 and 4
                Noise(data, t, 0.03f, 0.12f, rng);                   // hi-hat
                if (s % 2 == 0)
                {
                    var root = roots[(s / 8) % roots.Length];
                    Square(data, t, root, Step * 1.6f, 0.16f);
                }
            }
            foreach (var n in _notes)
            {
                Click(data, LeadIn + n.Time, n.Kind == NoteKind.Slider ? 659.25f : 880f, 0.12f, 0.18f);
                if (n.Kind == NoteKind.Slider) Click(data, LeadIn + n.EndTime, 987.77f, 0.08f, 0.12f);   // slider end
            }
            for (var i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("gc26_timetraveler_boss_song", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Kick(float[] d, float at)
        {
            var start = (int)(at * Rate);
            var n = (int)(0.16f * Rate);
            var phase = 0f;
            for (var i = 0; i < n && start + i < d.Length; i++)
            {
                var t = i / (float)Rate;
                var f = Mathf.Lerp(130f, 45f, t / 0.16f);
                phase += f / Rate;
                d[start + i] += Mathf.Sin(phase * Mathf.PI * 2f) * 0.7f * (1f - t / 0.16f);
            }
        }

        static void Noise(float[] d, float at, float len, float vol, System.Random rng)
        {
            var start = (int)(at * Rate);
            var n = (int)(len * Rate);
            for (var i = 0; i < n && start + i < d.Length; i++)
                d[start + i] += ((float)rng.NextDouble() * 2f - 1f) * vol * (1f - i / (float)n);
        }

        static void Square(float[] d, float at, float freq, float len, float vol)
        {
            var start = (int)(at * Rate);
            var n = (int)(len * Rate);
            for (var i = 0; i < n && start + i < d.Length; i++)
            {
                var wave = Mathf.Repeat(i * freq / Rate, 1f) < 0.5f ? 1f : -1f;
                d[start + i] += wave * vol * Mathf.Exp(-3f * i / (float)n);
            }
        }

        static void Click(float[] d, float at, float freq, float len, float vol)
        {
            var start = (int)(at * Rate);
            var n = (int)(len * Rate);
            for (var i = 0; i < n && start + i < d.Length; i++)
                d[start + i] += Mathf.Sin(i * freq / Rate * Mathf.PI * 2f) * vol * Mathf.Exp(-8f * i / (float)n);
        }

        static AudioClip Blip(float freq, float len, float vol)
        {
            var data = new float[(int)(len * Rate)];
            Click(data, 0f, freq, len, vol);
            var clip = AudioClip.Create("gc26_timetraveler_boss_blip", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (_song != null) Destroy(_song);
            if (_hitClip != null) Destroy(_hitClip);
            if (_missClip != null) Destroy(_missClip);

        }
    }
}
