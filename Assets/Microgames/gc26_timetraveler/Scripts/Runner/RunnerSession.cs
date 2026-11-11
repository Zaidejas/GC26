using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Greybox runner session: creates the traveler, track and camera follow, ramps the speed,
    /// runs the two mechanics (time slow and world switch), shows a debug HUD, and restarts on tap
    /// after a death. Everything lives under this object inside the game's own scene
    /// (no DontDestroyOnLoad, no static state). Time.timeScale is always restored (rule 6).
    /// </summary>
    [DefaultExecutionOrder(-50)] // read input before the traveler's Update
    public class RunnerSession : MonoBehaviour
    {
        [Header("Speed (units per second)")]
        public float startSpeed = 7.5f;
        public float maxSpeed = 12f;
        public float speedRampPerSecond = 0.06f;

        [Tooltip("Units of track built ahead of the right screen edge")]
        public float buildAhead = 40f;
        [Tooltip("Seconds after death before a tap restarts")]
        public float restartDelay = 0.4f;
        [Tooltip("Seconds the controls hint stays on screen at the start of a run")]
        public float hintDuration = 8f;

        /// <summary>1 unit = 16 px; the 3-unit tall traveler is about 1.8 m, so 1 unit is about 0.6 m.</summary>
        public const float MetersPerUnit = 0.6f;

        static readonly Color PastBackground = new Color(0.16f, 0.13f, 0.1f);
        static readonly Color FutureBackground = new Color(0.07f, 0.12f, 0.2f);

        static string BestKey => MicrogameSave.Key(TimetravelerGame.Id, "runner_best");

        RunnerShapes _shapes;
        TravelerInput _input;
        TravelerController _traveler;
        RunnerTrack _track;
        RunnerCamera _follow;
        Camera _camera;
        readonly RunnerWorldState _worlds = new RunnerWorldState();
        readonly RunnerTimeSlow _timeSlow = new RunnerTimeSlow();

        float _runTime;
        float _deathTime;
        float _blockedFlash;
        float _switchFlash;
        SpriteRenderer _travelerVisual;
        int _seed;
        int _bestMeters;
        GUIStyle _bigStyle;
        GUIStyle _smallStyle;
        GUIStyle _centerStyle;

        public int DistanceMeters =>
            _traveler == null ? 0 : Mathf.Max(0, Mathf.RoundToInt((_traveler.transform.position.x - _track.StartX) * MetersPerUnit));

        public void Init(Camera cam)
        {
            _camera = cam;
            _shapes = new RunnerShapes();
            _input = new TravelerInput();
            _bestMeters = PlayerPrefs.GetInt(BestKey, 0);
            _worlds.Changed += OnWorldChanged;

            _track = new GameObject("TimetravelerTrack").AddComponent<RunnerTrack>();
            _track.transform.SetParent(transform, false);

            var travelerObject = new GameObject("TimetravelerTraveler");
            travelerObject.transform.SetParent(transform, false);
            travelerObject.AddComponent<Rigidbody2D>();
            travelerObject.AddComponent<BoxCollider2D>();
            _traveler = travelerObject.AddComponent<TravelerController>();
            var visual = _shapes.Visual(travelerObject.transform, "TravelerBody", _traveler.tuning.standSize,
                RunnerShapes.TravelerColor, 2);
            _travelerVisual = visual;
            _traveler.Init(_input, visual.transform, _track.KillY);
            _traveler.Died += OnDied;

            _follow = cam.GetComponent<RunnerCamera>();
            if (_follow == null) _follow = cam.gameObject.AddComponent<RunnerCamera>();

            StartRun();
            _follow.Init(cam, _traveler.transform);
        }

        void StartRun()
        {
            _seed++;
            _runTime = 0f;
            _timeSlow.Reset();
            _worlds.Reset();
            _track.Build(_shapes, _worlds, _traveler.transform, _seed);
            _traveler.ResetAt(new Vector2(_track.StartX, 0f), startSpeed);
            if (_follow != null) _follow.Snap();
        }

        void Update()
        {
            if (_traveler == null) return;
            _input.Update();
            _blockedFlash -= Time.unscaledDeltaTime;
            _switchFlash -= Time.unscaledDeltaTime;

            if (_traveler.IsDead)
            {
                if (_input.AnyPressed && Time.unscaledTime - _deathTime > restartDelay) StartRun();
                return;
            }

            if (_input.SlowPressed) _timeSlow.Toggle();
            _timeSlow.Tick(Time.unscaledDeltaTime);

            if (_input.SwitchPressed) TrySwitchWorld();

            _runTime += Time.deltaTime;
            _traveler.RunSpeed = Mathf.Min(maxSpeed, startSpeed + speedRampPerSecond * _runTime);
            _track.Extend(_follow.RightEdge + buildAhead);
            _track.Trim(_follow.LeftEdge - 10f);
        }

        /// <summary>Switch timelines, unless it would trap the traveler inside a solid of the other world.</summary>
        void TrySwitchWorld()
        {
            if (!_worlds.CanSwitch) return;
            if (_track.SolidOverlaps(_traveler.BodyBounds, _worlds.Other))
            {
                _blockedFlash = 0.6f;
                return;
            }
            _worlds.Switch();
            _switchFlash = 0.6f;
        }

        void OnWorldChanged(RunnerWorld world)
        {
            if (_camera != null)
                _camera.backgroundColor = world == RunnerWorld.Past ? PastBackground : FutureBackground;
            // The traveler wears the colour of his world: same colour = solid for him.
            if (_travelerVisual != null) _travelerVisual.color = RunnerWorldState.ColorOf(world);
        }

        void OnDied()
        {
            _timeSlow.Restore();
            _deathTime = Time.unscaledTime;
            var meters = DistanceMeters;
            if (meters > _bestMeters)
            {
                _bestMeters = meters;
                PlayerPrefs.SetInt(BestKey, _bestMeters);
                PlayerPrefs.Save();
            }
        }

        void OnDisable()
        {
            _timeSlow.Restore();
        }

        void OnDestroy()
        {
            _timeSlow.Restore();
            if (_traveler != null) _traveler.Died -= OnDied;
            _worlds.Changed -= OnWorldChanged;
        }

        // ---------- temporary debug HUD (real UI comes later) ----------

        void OnGUI()
        {
            if (_traveler == null) return;
            if (_bigStyle == null)
            {
                _bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
                _centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            }
            var unit = Screen.height / 40f;
            _bigStyle.fontSize = Mathf.RoundToInt(unit * 1.4f);
            _smallStyle.fontSize = Mathf.RoundToInt(unit * 0.8f);
            _centerStyle.fontSize = Mathf.RoundToInt(unit * 0.7f);

            var safe = Screen.safeArea;
            var left = safe.x + unit;
            var y = Screen.height - safe.yMax + unit * 0.5f;
            var width = safe.width - unit * 2f;

            GUI.Label(new Rect(left, y, width, unit * 1.2f),
                $"{DistanceMeters} m   best {_bestMeters} m   speed {_traveler.RunSpeed:0.0}", _smallStyle);
            y += unit * 1.3f;

            // Current world, in its own colour.
            var oldColor = GUI.color;
            GUI.color = RunnerWorldState.ColorOf(_worlds.Current);
            GUI.Label(new Rect(left, y, width, unit * 1.2f),
                $"World: {RunnerWorldState.NameOf(_worlds.Current)}  (your colour = solid)", _smallStyle);
            GUI.color = oldColor;
            y += unit * 1.3f;

            // Watch energy bar.
            GUI.Label(new Rect(left, y, width, unit * 1.2f),
                _timeSlow.Active ? "Time slow: ON  (E / Shift to stop)" : "Time slow  (E / Shift)", _smallStyle);
            var barY = y + unit * 1.2f;
            var barWidth = width * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(left, barY, barWidth, unit * 0.4f), Texture2D.whiteTexture);
            GUI.color = _timeSlow.Energy >= _timeSlow.minEnergyToStart ? new Color(0.4f, 0.85f, 1f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.DrawTexture(new Rect(left, barY, barWidth * _timeSlow.Energy, unit * 0.4f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            // Big world name right after a switch.
            if (_switchFlash > 0f && !_traveler.IsDead)
            {
                GUI.color = RunnerWorldState.ColorOf(_worlds.Current);
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, unit * 2f),
                    RunnerWorldState.NameOf(_worlds.Current), _bigStyle);
                GUI.color = oldColor;
            }
            if (_blockedFlash > 0f && !_traveler.IsDead)
            {
                GUI.color = new Color(1f, 0.4f, 0.35f);
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, unit * 2f),
                    "Can't switch here: you'd be inside a wall", _centerStyle);
                GUI.color = oldColor;
            }

            // Hint for an obstacle met for the first time.
            var hint = _traveler.IsDead ? null : _track.HintFor(_traveler.transform.position.x);
            if (hint != null)
            {
                var box = new Rect(safe.x + unit * 0.5f, Screen.height * 0.28f, safe.width - unit, unit * 3.2f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(box.x + unit * 0.3f, box.y + unit * 0.3f, box.width - unit * 0.6f, box.height), hint, _centerStyle);
                GUI.color = oldColor;
            }

            // Controls legend: always visible during the greybox phase.
            var legend = _runTime < hintDuration
                ? "Tap: jump (tap again = double)   Swipe down: slide\nSwipe up: slow time   Swipe sideways: switch world\nKeys: Space jump  S slide  E/Shift slow  Q switch"
                : "Space jump   S slide   E/Shift slow   Q switch";
            GUI.Label(new Rect(safe.x + unit, Screen.height - safe.y - unit * 3.4f, width, unit * 3.2f), legend, _centerStyle);

            if (_traveler.IsDead)
            {
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, unit * 2f), $"{DistanceMeters} m", _bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.35f + unit * 2.2f, Screen.width, unit * 2f), "Tap to run again", _bigStyle);
            }
        }
    }
}
