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

            if (_traveler.IsDead)
            {
                if (_input.AnyPressed && Time.unscaledTime - _deathTime > restartDelay) StartRun();
                return;
            }

            if (_input.SlowPressed) _timeSlow.Toggle();
            _timeSlow.Tick(Time.unscaledDeltaTime);

            _worlds.Tick(Time.deltaTime);
            if (_input.SwitchPressed) TrySwitchWorld();

            _runTime += Time.deltaTime;
            _traveler.RunSpeed = Mathf.Min(maxSpeed, startSpeed + speedRampPerSecond * _runTime);
            _track.Extend(_follow.RightEdge + buildAhead);
            _track.Trim(_follow.LeftEdge - 10f);
        }

        /// <summary>Switch timelines, unless out of charges or it would trap the traveler inside a solid.</summary>
        void TrySwitchWorld()
        {
            if (!_worlds.CanSwitch || _track.SolidOverlaps(_traveler.BodyBounds, _worlds.Other))
            {
                _blockedFlash = 0.35f;
                return;
            }
            _worlds.Switch();
        }

        void OnWorldChanged(RunnerWorld world)
        {
            if (_camera != null)
                _camera.backgroundColor = world == RunnerWorld.Past ? PastBackground : FutureBackground;
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

            // World and switch charges.
            var worldName = _worlds.Current == RunnerWorld.Past ? "PAST" : "FUTURE";
            var charges = Mathf.FloorToInt(_worlds.Charges);
            var chargeText = new string('#', charges) + new string('.', RunnerWorldState.MaxCharges - charges);
            var oldColor = GUI.color;
            if (_blockedFlash > 0f) GUI.color = new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(left, y, width, unit * 1.2f), $"World: {worldName}   switches [{chargeText}]", _smallStyle);
            GUI.color = oldColor;
            y += unit * 1.3f;

            // Watch energy bar.
            GUI.Label(new Rect(left, y, width, unit * 1.2f), _timeSlow.Active ? "Time slow: ON" : "Time slow", _smallStyle);
            var barY = y + unit * 1.2f;
            var barWidth = width * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(left, barY, barWidth, unit * 0.4f), Texture2D.whiteTexture);
            GUI.color = _timeSlow.Energy >= _timeSlow.minEnergyToStart ? new Color(0.4f, 0.85f, 1f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.DrawTexture(new Rect(left, barY, barWidth * _timeSlow.Energy, unit * 0.4f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            if (_runTime < hintDuration && !_traveler.IsDead)
            {
                GUI.Label(new Rect(safe.x + unit, Screen.height - safe.y - unit * 4.5f, width, unit * 4f),
                    "Tap: jump (hold = higher, tap again = double)\nSwipe down: slide   Swipe up: slow time\nSwipe left/right: switch world\n(Keys: Space, S, E, Q)",
                    _centerStyle);
            }

            if (_traveler.IsDead)
            {
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, unit * 2f), $"{DistanceMeters} m", _bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.35f + unit * 2.2f, Screen.width, unit * 2f), "Tap to run again", _bigStyle);
            }
        }
    }
}
