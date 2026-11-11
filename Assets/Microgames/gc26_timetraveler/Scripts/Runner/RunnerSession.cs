using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Runner session: creates the traveler, track and camera follow, ramps the speed,
    /// runs the two mechanics (time slow and world switch), coins, and a debug HUD.
    /// Story level: fixed track with a finish line and a results panel. Endless: new track every run.
    /// Restarts on tap after a death. Everything lives under this object inside the game's own scene
    /// (no DontDestroyOnLoad, no static state). Time.timeScale is always restored (rule 6).
    /// </summary>
    [DefaultExecutionOrder(-50)] // read input before the traveler's Update
    public class RunnerSession : MonoBehaviour
    {
        [Header("Speed (units per second)")]
        public float startSpeed = 7f;
        public float maxSpeed = 12f;
        [Tooltip("Top speed is reached after (max - start) / ramp seconds: about a minute")]
        public float speedRampPerSecond = 0.08f;

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
        SpriteRenderer _travelerVisual;   // plain box, only if the sprites are missing
        TravelerAnimator _travelerSprite;

        bool _paused;
        float _scaleBeforePause = 1f;

        /// <summary>The player wants to leave the run (pause "To map/menu", or "Continue" after the finish).</summary>
        public System.Action LeaveRequested;
        int _seed;
        TimetravelerSave _save;
        TimetravelerLevel _level;       // null = endless
        int _runCoins;
        bool _coinsBanked;
        bool _finished;
        bool _newRecord;
        SpriteRenderer _background;

        // ---- feel (effects only, no gameplay) ----
        RunnerFx _fx;
        float _dustTimer;
        int _speedStep;
        float _speedFlash;
        static readonly Color DustColor = new Color(0.78f, 0.68f, 0.52f, 0.9f);
        static readonly Color SparkColor = new Color(1f, 0.9f, 0.35f);
        GUIStyle _bigStyle;
        GUIStyle _smallStyle;
        GUIStyle _centerStyle;
        readonly TimetravelerUi _ui = new TimetravelerUi();

        public int DistanceMeters =>
            _traveler == null ? 0 : Mathf.Max(0, Mathf.RoundToInt((_traveler.transform.position.x - _track.StartX) * MetersPerUnit));

        public bool IsEndless => _level == null;

        /// <param name="level">Story level to play, or null for the endless run.</param>
        public void Init(Camera cam, TimetravelerSave save, TimetravelerLevel level)
        {
            _camera = cam;
            _save = save;
            _level = level;
            _shapes = new RunnerShapes();
            _input = new TravelerInput();
            _worlds.Changed += OnWorldChanged;

            var bg = new GameObject("RunnerBackground");
            bg.transform.SetParent(transform, false);
            _background = bg.AddComponent<SpriteRenderer>();
            _background.sharedMaterial = _shapes.Material;
            _background.sortingOrder = -100;

            _track = new GameObject("TimetravelerTrack").AddComponent<RunnerTrack>();
            _track.transform.SetParent(transform, false);

            var travelerObject = new GameObject("TimetravelerTraveler");
            travelerObject.transform.SetParent(transform, false);
            travelerObject.AddComponent<Rigidbody2D>();
            travelerObject.AddComponent<BoxCollider2D>();
            _traveler = travelerObject.AddComponent<TravelerController>();
            _travelerSprite = TravelerAnimator.TryCreate(_traveler, _shapes.Material);
            Transform boxVisual = null;
            if (_travelerSprite == null)
            {
                _travelerVisual = _shapes.Visual(travelerObject.transform, "TravelerBody", _traveler.tuning.standSize,
                    RunnerShapes.TravelerColor, 2);
                boxVisual = _travelerVisual.transform;
            }
            _traveler.Init(_input, boxVisual, _track.KillY);
            _traveler.Died += OnDied;
            _traveler.CoinTouched += OnCoin;
            _traveler.Jumped += OnJumped;
            _traveler.Landed += OnLanded;

            _fx = new GameObject("RunnerFx").AddComponent<RunnerFx>();
            _fx.transform.SetParent(transform, false);
            _fx.Init(_shapes);

            // Our own follow component (removed again in OnDestroy, so the map can move the camera).
            _follow = cam.gameObject.AddComponent<RunnerCamera>();

            TimetravelerLog.Info(IsEndless ? $"Endless run created (best so far {_save.BestMeters} m)."
                : $"Level {_level.Title} created ({_level.LengthMeters} m, tier {_level.Tier}, seed {_level.Seed}).", this);
            StartRun();
            _follow.Init(cam, _traveler.transform);
        }

        void StartRun()
        {
            _seed = IsEndless ? _seed + 1 : _level.Seed;   // a level is the same track every attempt
            _runTime = 0f;
            _runCoins = 0;
            _coinsBanked = false;
            _finished = false;
            _newRecord = false;
            _timeSlow.Reset();
            if (_fx != null) _fx.Clear();
            _speedStep = Mathf.FloorToInt(startSpeed);
            _speedFlash = 0f;
            _worlds.Reset();
            if (IsEndless) _track.Build(_shapes, _worlds, _traveler.transform, _seed);
            else _track.Build(_shapes, _worlds, _traveler.transform, _seed, _level.Tier, _level.LengthMeters);
            _traveler.ResetAt(new Vector2(_track.StartX, 0f), startSpeed);
            if (_follow != null) _follow.Snap();
            TimetravelerLog.Info($"Run started (track seed {_seed}, speed {startSpeed}).", this);
        }

        void Update()
        {
            if (_traveler == null) return;
            _input.IgnoreRect = PauseButtonRectInput();
            _input.Update();
            _ui.Update();

            if (_input.PausePressed)
            {
                if (_paused) Resume();
                else if (!_traveler.IsDead) Pause();
            }
            if (_paused) return;

            _blockedFlash -= Time.unscaledDeltaTime;
            _switchFlash -= Time.unscaledDeltaTime;

            if (_traveler.IsDead)
            {
                if (_input.AnyPressed && Time.unscaledTime - _deathTime > restartDelay) StartRun();
                return;
            }

            if (_finished)
            {
                // Jog on past the finish while the results are shown.
                _traveler.RunSpeed = startSpeed;
                _track.Extend(_follow.RightEdge + buildAhead);
                return;
            }
            if (_traveler.transform.position.x >= _track.FinishX)
            {
                Finish();
                return;
            }

            if (_input.SlowPressed)
            {
                var wasActive = _timeSlow.Active;
                _timeSlow.Toggle();
                if (_timeSlow.Active != wasActive)
                    TimetravelerLog.Info($"Time slow {(_timeSlow.Active ? "ON" : "OFF")} (energy {_timeSlow.Energy:P0}).", this);
                else
                    TimetravelerLog.Info($"Time slow refused: energy {_timeSlow.Energy:P0}, needs {_timeSlow.minEnergyToStart:P0}.", this);
            }
            var slowBefore = _timeSlow.Active;
            _timeSlow.Tick(Time.unscaledDeltaTime);
            if (slowBefore && !_timeSlow.Active) TimetravelerLog.Info("Time slow OFF (watch energy ran out).", this);

            if (_input.SwitchPressed) TrySwitchWorld();

            _runTime += Time.deltaTime;
            var baseSpeed = Mathf.Min(maxSpeed, startSpeed + speedRampPerSecond * _runTime);
            _traveler.RunSpeed = baseSpeed * _timeSlow.TravelerSpeedFactor;
            UpdateFeel(baseSpeed);
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
                TimetravelerLog.Info($"World switch refused at {DistanceMeters} m: would be inside a {_worlds.Other} solid.", this);
                return;
            }
            _worlds.Switch();
            _switchFlash = 0.6f;
            _fx.Burst(_traveler.transform.position, 14, RunnerWorldState.ColorOf(_worlds.Current), Vector2.zero, 8f, 0.35f, 0.45f,
                falls: false);
            _follow.Shake(0.12f, 0.12f);
            TimetravelerLog.Info($"World switched to {RunnerWorldState.NameOf(_worlds.Current)} at {DistanceMeters} m.", this);
        }

        void OnWorldChanged(RunnerWorld world)
        {
            if (_camera != null)
                _camera.backgroundColor = world == RunnerWorld.Past ? PastBackground : FutureBackground;
            // The traveler wears the colour of his world: same colour = solid for him.
            if (_travelerVisual != null) _travelerVisual.color = RunnerWorldState.ColorOf(world);
            if (_travelerSprite != null) _travelerSprite.SetWorldColor(RunnerWorldState.ColorOf(world));
        }

        void OnDied(string reason)
        {
            _timeSlow.Restore();
            _deathTime = Time.unscaledTime;
            _fx.Burst(_traveler.transform.position, 18, RunnerShapes.HazardColor, new Vector2(0f, 6f), 8f, 0.45f, 0.8f);
            _follow.Shake(0.5f, 0.35f);
            TimetravelerLog.Info($"Died at {DistanceMeters} m: {reason} (world {RunnerWorldState.NameOf(_worlds.Current)}, speed {_traveler.RunSpeed:0.0}).", this);
            EndRunBookkeeping();
        }

        void OnCoin(RunnerCoin coin)
        {
            _runCoins++;
            _fx.Burst(coin.transform.position, 5, SparkColor, new Vector2(0f, 3f), 3f, 0.25f, 0.35f, falls: false);
            Destroy(coin.gameObject);
        }

        Vector2 Feet => (Vector2)_traveler.transform.position - new Vector2(0f, _traveler.ColliderHeight / 2f);

        void OnJumped(bool isDouble)
        {
            if (_travelerSprite != null) _travelerSprite.Punch(0.8f, 1.25f);
            if (isDouble)
                _fx.Burst(Feet, 8, RunnerWorldState.ColorOf(_worlds.Current), new Vector2(0f, -4f), 4f, 0.3f, 0.35f, falls: false);
            else
                _fx.Burst(Feet, 5, DustColor, new Vector2(-2f, 1.5f), 2f, 0.3f, 0.35f);
        }

        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < 6f) return;
            if (_travelerSprite != null) _travelerSprite.Punch(1.3f, 0.75f);
            _fx.Burst(Feet, 7, DustColor, new Vector2(0f, 2.5f), 4f, 0.32f, 0.4f);
            if (impactSpeed > 30f) _follow.Shake(0.15f, 0.12f);
        }

        /// <summary>Running dust, camera lead that grows with speed, "SPEED UP" message.</summary>
        void UpdateFeel(float baseSpeed)
        {
            var speed01 = Mathf.InverseLerp(startSpeed, maxSpeed, baseSpeed);
            _follow.screenAnchorX = Mathf.Lerp(0.22f, 0.13f, speed01);   // see further ahead when fast

            _speedFlash -= Time.unscaledDeltaTime;
            var step = Mathf.FloorToInt(baseSpeed);
            if (step > _speedStep)
            {
                _speedStep = step;
                _speedFlash = 1.2f;
            }

            if (!_traveler.IsGrounded) return;
            _dustTimer -= Time.deltaTime;
            if (_dustTimer > 0f) return;
            _dustTimer = _traveler.IsSliding ? 0.04f : Mathf.Lerp(0.16f, 0.08f, speed01);
            _fx.Burst(Feet + new Vector2(-0.5f, 0.1f), _traveler.IsSliding ? 2 : 1, DustColor,
                new Vector2(-_traveler.RunSpeed * 0.25f, 1.2f), 0.8f, 0.25f, 0.35f);
        }

        void Finish()
        {
            _finished = true;
            _timeSlow.Restore();
            _save.CompleteLevel(_level, _runCoins);
            EndRunBookkeeping();
            TimetravelerLog.Info($"Level {_level.Title} finished with {_runCoins} coins.", this);
        }

        /// <summary>Puts this run's coins in the bank (once) and records the endless distance.</summary>
        void EndRunBookkeeping()
        {
            if (!_coinsBanked)
            {
                _save.AddCoins(_runCoins);
                _coinsBanked = true;
            }
            if (IsEndless && _save.ReportDistance(DistanceMeters))
            {
                _newRecord = true;
                TimetravelerLog.Info($"New best distance: {_save.BestMeters} m.", this);
            }
        }

        void LateUpdate()
        {
            if (_background == null || _camera == null) return;
            var slot = _worlds.Current == RunnerWorld.Future ? TimetravelerArt.RunnerBackgroundFuture : TimetravelerArt.RunnerBackgroundPast;
            if (_backgroundSlot != slot)
            {
                _backgroundSlot = slot;
                _background.sprite = TimetravelerArt.Sprite(slot) ?? TimetravelerArt.Sprite(TimetravelerArt.RunnerBackgroundPast);
            }
            // A 270x480 px picture at 16 px per unit fills the view exactly; it follows the camera.
            var p = _camera.transform.position;
            _background.transform.position = new Vector3(p.x, p.y, 0f);
        }

        string _backgroundSlot;

        // ---------- pause and exit (required by GC26: own pause + "To menu") ----------

        public void Pause()
        {
            if (_paused) return;
            _paused = true;
            _scaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            TimetravelerLog.Info($"Paused at {DistanceMeters} m.", this);
        }

        public void Resume()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = _scaleBeforePause;
            TimetravelerLog.Info("Resumed.", this);
        }

        /// <summary>Banks the coins, restores time and asks the game flow to leave this run.</summary>
        void Leave()
        {
            Resume();
            _timeSlow.Restore();
            if (!_traveler.IsDead && !_finished) EndRunBookkeeping();
            TimetravelerLog.Info($"Leaving the run ({(IsEndless ? "to the menu" : "to the map")}).", this);
            LeaveRequested?.Invoke();
        }

        Rect PauseButtonRectGui()
        {
            var unit = Screen.height / 40f;
            var safe = Screen.safeArea;
            var size = unit * 2.4f;
            return new Rect(safe.xMax - size - unit * 0.5f, Screen.height - safe.yMax + unit * 0.5f, size, size);
        }

        /// <summary>Same rect in Input System coordinates (origin bottom-left).</summary>
        Rect PauseButtonRectInput()
        {
            var r = PauseButtonRectGui();
            return new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
        }

        void OnDisable()
        {
            Resume();
            _timeSlow.Restore();
        }

        void OnDestroy()
        {
            Resume();
            _timeSlow.Restore();
            if (_traveler != null)
            {
                _traveler.Died -= OnDied;
                _traveler.CoinTouched -= OnCoin;
                _traveler.Jumped -= OnJumped;
                _traveler.Landed -= OnLanded;
            }
            _worlds.Changed -= OnWorldChanged;
            // The map and menu move the camera themselves.
            if (_follow != null) Destroy(_follow);
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

            var progress = IsEndless
                ? $"{DistanceMeters} m   best {_save.BestMeters} m"
                : $"Level {_level.Title}   {Mathf.Min(DistanceMeters, _level.LengthMeters)} / {_level.LengthMeters} m";
            GUI.Label(new Rect(left, y, width, unit * 1.2f), $"{progress}   coins {_runCoins}", _smallStyle);
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

            if (_speedFlash > 0f && !_traveler.IsDead && !_finished)
            {
                GUI.color = new Color(1f, 0.9f, 0.4f, Mathf.Clamp01(_speedFlash));
                GUI.Label(new Rect(0, Screen.height * 0.13f, Screen.width, unit * 2f), "SPEED UP!", _bigStyle);
                GUI.color = oldColor;
            }

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
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, unit * 2f),
                    _newRecord ? $"{DistanceMeters} m  NEW BEST!" : $"{DistanceMeters} m   +{_runCoins} coins", _bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.35f + unit * 2.2f, Screen.width, unit * 2f),
                    IsEndless ? "Tap to run again" : "Tap to try again", _bigStyle);
            }

            if (_finished)
            {
                DrawResults(unit);
                return;
            }

            DrawPause(unit);
        }

        void DrawPause(float unit)
        {
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(unit * 0.9f), fontStyle = FontStyle.Bold };

            if (!_paused)
            {
                if (!_traveler.IsDead && _ui.Button(PauseButtonRectGui(), "II", buttonStyle)) Pause();
                return;
            }

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, unit * 2f), "PAUSED", _bigStyle);
            var w = Mathf.Min(Screen.width * 0.6f, unit * 12f);
            var x = (Screen.width - w) / 2f;
            var h = unit * 2.4f;
            if (_ui.Button(new Rect(x, Screen.height * 0.42f, w, h), "Resume", buttonStyle)) Resume();
            if (_ui.Button(new Rect(x, Screen.height * 0.42f + h + unit, w, h), IsEndless ? "To menu" : "To map", buttonStyle)) Leave();
        }

        void DrawResults(float unit)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, unit * 2f), $"LEVEL {_level.Title} COMPLETE", _bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.28f + unit * 2.4f, Screen.width, unit * 2f),
                $"Coins: {_runCoins}   (best {_save.LevelBestCoins(_level)})", _bigStyle);
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(unit * 0.9f), fontStyle = FontStyle.Bold };
            var w = Mathf.Min(Screen.width * 0.6f, unit * 12f);
            var x = (Screen.width - w) / 2f;
            var h = unit * 2.4f;
            if (_ui.Button(new Rect(x, Screen.height * 0.45f, w, h), "Continue", buttonStyle)) Leave();
            if (_ui.Button(new Rect(x, Screen.height * 0.45f + h + unit, w, h), "Play again", buttonStyle)) StartRun();
        }
    }
}
