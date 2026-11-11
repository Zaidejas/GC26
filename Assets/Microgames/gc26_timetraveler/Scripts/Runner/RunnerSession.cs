using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Greybox runner session: creates the traveler, track and camera follow, ramps the speed,
    /// shows distance, and restarts on tap after a death. Everything lives under this object,
    /// inside the game's own scene (no DontDestroyOnLoad, no static state).
    /// </summary>
    [DefaultExecutionOrder(-50)] // read input before the traveler's Update
    public class RunnerSession : MonoBehaviour
    {
        [Header("Speed (units per second)")]
        public float startSpeed = 9f;
        public float maxSpeed = 14f;
        public float speedRampPerSecond = 0.08f;

        [Tooltip("Units of track built ahead of the right screen edge")]
        public float buildAhead = 40f;
        [Tooltip("Seconds after death before a tap restarts")]
        public float restartDelay = 0.4f;

        /// <summary>1 unit = 16 px; the 3-unit tall traveler is about 1.8 m, so 1 unit is about 0.6 m.</summary>
        const float MetersPerUnit = 0.6f;

        static string BestKey => MicrogameSave.Key(TimetravelerGame.Id, "runner_best");

        RunnerShapes _shapes;
        TravelerInput _input;
        TravelerController _traveler;
        RunnerTrack _track;
        RunnerCamera _follow;

        float _runTime;
        float _deathTime;
        int _seed;
        int _bestMeters;
        GUIStyle _bigStyle;
        GUIStyle _smallStyle;

        public int DistanceMeters =>
            _traveler == null ? 0 : Mathf.Max(0, Mathf.RoundToInt((_traveler.transform.position.x - _track.StartX) * MetersPerUnit));

        public void Init(Camera cam)
        {
            _shapes = new RunnerShapes();
            _input = new TravelerInput();
            _bestMeters = PlayerPrefs.GetInt(BestKey, 0);

            _track = new GameObject("TimetravelerTrack").AddComponent<RunnerTrack>();
            _track.transform.SetParent(transform, false);

            var travelerObject = new GameObject("TimetravelerTraveler");
            travelerObject.transform.SetParent(transform, false);
            travelerObject.AddComponent<Rigidbody2D>();
            travelerObject.AddComponent<BoxCollider2D>();
            _traveler = travelerObject.AddComponent<TravelerController>();
            var visual = _shapes.Visual(travelerObject.transform, "TravelerBody", _traveler.tuning.standSize,
                RunnerShapes.TravelerColor, 2);
            // Visual is a child; undo the parent's scale (parent stays at 1,1,1).
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
            _track.Build(_shapes, _seed);
            _traveler.ResetAt(new Vector2(_track.StartX, 0f), startSpeed);
            if (_follow != null) _follow.Snap();
        }

        void Update()
        {
            if (_traveler == null) return;
            _input.Update();

            if (_traveler.IsDead)
            {
                if (_input.AnyPressed && Time.time - _deathTime > restartDelay) StartRun();
                return;
            }

            _runTime += Time.deltaTime;
            _traveler.RunSpeed = Mathf.Min(maxSpeed, startSpeed + speedRampPerSecond * _runTime);
            _track.Extend(_follow.RightEdge + buildAhead);
            _track.Trim(_follow.LeftEdge - 10f);
        }

        void OnDied()
        {
            _deathTime = Time.time;
            var meters = DistanceMeters;
            if (meters > _bestMeters)
            {
                _bestMeters = meters;
                PlayerPrefs.SetInt(BestKey, _bestMeters);
                PlayerPrefs.Save();
            }
        }

        void OnDestroy()
        {
            if (_traveler != null) _traveler.Died -= OnDied;
        }

        // Temporary debug HUD until the real UI (week 3).
        void OnGUI()
        {
            if (_traveler == null) return;
            if (_bigStyle == null)
            {
                _bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            }
            var unit = Screen.height / 40f;
            _bigStyle.fontSize = Mathf.RoundToInt(unit * 1.4f);
            _smallStyle.fontSize = Mathf.RoundToInt(unit * 0.9f);

            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax + unit * 0.5f;
            GUI.Label(new Rect(safe.x + unit, top, Screen.width, unit * 1.5f),
                $"{DistanceMeters} m   best {_bestMeters} m   speed {_traveler.RunSpeed:0.0}", _smallStyle);

            if (_traveler.IsDead)
            {
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, unit * 2f), $"{DistanceMeters} m", _bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.35f + unit * 2.2f, Screen.width, unit * 2f), "Tap to run again", _bigStyle);
            }
        }
    }
}
