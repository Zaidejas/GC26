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

        static readonly Color FutureBackground = new Color(0.07f, 0.12f, 0.2f);
        TimetravelerEra Era => TimetravelerLevels.Eras[_level != null ? _level.Era : 0];
        Color PastBackground => Era.Sky;

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
        RunnerParallax _parallax;
        RunnerBoss _boss;               // boss levels only
        TimetravelerSettings _settings;

        // ---- tutorial: the first time an obstacle type comes, time slows down and an example plays ----
        const float TutorialTimeScale = 0.3f;
        RunnerTutorial _tutorial;
        string _tutorialKey;            // the obstacle being taught in slow motion (null = none)
        float _tutorialScaleBefore = 1f;
        bool _wasSliding;
        /// <summary>Tests: the bot playthroughs turn the slow motion off to run faster (one test checks it on).</summary>
        internal bool TutorialSlowMotion = true;
        internal bool InTutorialSlowMotion => _tutorialKey != null;

        // ---- checkpoints (levels) ----
        bool _checkpointReached;
        float _checkpointRunTime;
        float _checkpointFlash;

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

        // ---- for the automated tests (Tests/ folder) ----
        internal TravelerController Traveler => _traveler;
        internal RunnerTrack Track => _track;
        internal TravelerInput Input => _input;
        internal RunnerWorldState Worlds => _worlds;
        internal bool IsFinished => _finished;
        internal int RunCoins => _runCoins;
        internal bool CheckpointReached => _checkpointReached;
        internal RunnerBoss Boss => _boss;

        /// <param name="level">Story level to play, or null for the endless run.</param>
        /// <param name="settings">Player settings (null = defaults, e.g. in tests).</param>
        public void Init(Camera cam, TimetravelerSave save, TimetravelerLevel level, TimetravelerSettings settings = null)
        {
            _camera = cam;
            _save = save;
            _settings = settings ?? TimetravelerSettings.Defaults();
            TutorialSlowMotion = _settings.TutorialSlowMotion;
            _level = level;
            if (level != null)
            {
                startSpeed = level.StartSpeed;
                maxSpeed = level.MaxSpeed;
            }
            _shapes = new RunnerShapes();
            _input = new TravelerInput();
            _worlds.Changed += OnWorldChanged;

            var bg = new GameObject("RunnerBackground");
            bg.transform.SetParent(transform, false);
            _background = bg.AddComponent<SpriteRenderer>();
            _background.sharedMaterial = _shapes.Material;
            _background.sortingOrder = -100;

            _parallax = new GameObject("RunnerParallax").AddComponent<RunnerParallax>();
            _parallax.transform.SetParent(transform, false);
            _parallax.Init(cam, _shapes, level != null ? level.Seed : 7, Era.Hills, level != null ? level.Era : 0);

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

            _tutorial = new RunnerTutorial();
            _fx = new GameObject("RunnerFx").AddComponent<RunnerFx>();
            _fx.transform.SetParent(transform, false);
            _fx.Init(_shapes);

            // Our own follow component (removed again in OnDestroy, so the map can move the camera).
            _follow = cam.gameObject.AddComponent<RunnerCamera>();
            _follow.shakeFactor = _settings.ShakeFactor;

            if (level != null && level.Boss)
            {
                _boss = new GameObject("TimetravelerBoss").AddComponent<RunnerBoss>();
                _boss.transform.SetParent(transform, false);
                _boss.Init(_shapes, _fx, _follow, _traveler, _travelerSprite, _input, _track, _settings);
                _boss.Defeated += OnBossDefeated;
            }

            TimetravelerLog.Info(IsEndless ? $"Endless run created (best so far {_save.BestMeters} m)."
                : $"Level {_level.Title} created ({_level.LengthMeters} m, tier {_level.Tier}, seed {_level.Seed}).", this);
            StartRun();
            _follow.Init(cam, _traveler.transform);
        }

        void StartRun() => StartRun(false);

        /// <param name="fromCheckpoint">Levels: rebuild the same track and start at the half-way checkpoint.</param>
        void StartRun(bool fromCheckpoint)
        {
            EndTutorialSlowMotion(false);
            // A level is the same track every attempt; every endless run is a new random track.
            _seed = IsEndless ? Random.Range(1, int.MaxValue) : _level.Seed;
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
            else _track.Build(_shapes, _worlds, _traveler.transform, _seed, _level.Tier, _level.LengthMeters,
                _level.Density, _level.Hard, _level.Era, _level.Boss);
            if (_boss != null) _boss.ResetForRun();
            if (fromCheckpoint)
            {
                _track.Extend(_track.CheckpointMeters / MetersPerUnit + 60f);
                _runTime = _checkpointRunTime;
                _traveler.ResetAt(new Vector2(_track.CheckpointX + 1f, _track.CheckpointGroundY),
                    Mathf.Min(maxSpeed, startSpeed + speedRampPerSecond * _runTime));
                _checkpointReached = true;
                TimetravelerLog.Info("Continuing from the checkpoint.", this);
            }
            else
            {
                _checkpointReached = false;
                _traveler.ResetAt(new Vector2(_track.StartX, 0f), startSpeed);
            }
            if (_follow != null)
            {
                _follow.groundY = Feet.y;
                _follow.Snap();
            }
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
                if (_input.AnyPressed && Time.unscaledTime - _deathTime > restartDelay) StartRun(!IsEndless && _checkpointReached);
                return;
            }

            if (_finished)
            {
                // Jog on past the finish while the results are shown.
                _traveler.RunSpeed = startSpeed;
                _track.Extend(_follow.RightEdge + buildAhead);
                return;
            }
            if (!IsEndless && !_checkpointReached && _traveler.transform.position.x >= _track.CheckpointX)
            {
                _checkpointReached = true;
                _checkpointRunTime = _runTime;
                _checkpointFlash = 1.5f;
                _fx.Burst(_traveler.transform.position, 16, new Color(0.4f, 0.9f, 1f), new Vector2(0f, 4f), 6f, 0.35f, 0.6f, falls: false);
                TimetravelerLog.Info($"Checkpoint reached at {DistanceMeters} m.", this);
            }
            if (_traveler.transform.position.x >= _track.FinishX)
            {
                Finish();
                return;
            }
            if (_boss != null)
            {
                _boss.Tick();
                if (_boss.InControl)
                {
                    EndTutorialSlowMotion(true);
                    // The fight owns the input; the run is on hold (no speed, no world switch, no time slow).
                    _timeSlow.Restore();
                    _follow.screenAnchorX = _boss.CameraAnchor ?? _follow.screenAnchorX;
                    _track.Extend(_follow.RightEdge + buildAhead);
                    return;
                }
            }

            UpdateTutorial();
            if (_input.SlowPressed && _tutorialKey == null)
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
            if (_boss != null && _boss.CameraAnchor.HasValue) _follow.screenAnchorX = _boss.CameraAnchor.Value;
            _track.Extend(_follow.RightEdge + buildAhead);
            _track.Trim(_follow.LeftEdge - 10f);
        }

        // ---------- developer hotkeys (TimetravelerDevTools, editor and development builds only) ----------

        internal bool DevGodMode
        {
            get => _traveler != null && _traveler.Invulnerable;
            set { if (_traveler != null) _traveler.Invulnerable = value; }
        }

        internal TimetravelerLevel Level => _level;
        internal void DevRestart() => StartRun();

        /// <summary>Levels: start again at the checkpoint (on 1-5 that is where the boss chase begins).</summary>
        internal void DevSkipToCheckpoint()
        {
            if (IsEndless) return;
            _checkpointRunTime = Mathf.Max(_runTime, _level.LengthMeters * 0.5f / Mathf.Max(1f, startSpeed));
            _checkpointReached = true;
            StartRun(true);
        }

        /// <summary>Boss level: straight to the end of the chase, a few seconds before the cliff.</summary>
        internal void DevSkipToArena()
        {
            if (_boss == null) return;
            DevSkipToCheckpoint();
            _track.Extend(_track.CheckpointX + _level.LengthMeters / MetersPerUnit);
            if (float.IsInfinity(_track.ArenaX)) return;
            _traveler.ResetAt(new Vector2(_track.ArenaX - 25f, _track.ArenaGroundY), maxSpeed);
            _follow.groundY = _track.ArenaGroundY;
            _follow.Snap();
        }

        internal void DevFinish()
        {
            if (IsEndless || _finished || _traveler.IsDead) return;
            if (_boss != null && _boss.InControl) _boss.DevWin();
            else Finish();
        }

        internal void DevBossWin()
        {
            if (_boss != null) _boss.DevWin();
        }

        // ---------- tutorial slow motion ----------

        void UpdateTutorial()
        {
            var x = _traveler.transform.position.x;
            var sliding = _traveler.IsSliding;
            var startedSlide = sliding && !_wasSliding;
            _wasSliding = sliding;
            if (_tutorialKey != null && startedSlide) EndTutorialSlowMotion(true);   // the player did something

            if (!_track.CurrentHint(x, out var key, out _, out var ahead))
            {
                EndTutorialSlowMotion(true);
                return;
            }
            if (_tutorialKey != null)
            {
                // Passed it (or a new hint took over): back to normal speed.
                if (key != _tutorialKey || ahead < -0.5f) EndTutorialSlowMotion(true);
                return;
            }
            if (!TutorialSlowMotion || _save.TutorialSeen(key) || RunnerTutorial.MoveFor(key) == TutorialMove.None) return;
            // Slow down while it comes into reach, so there is time to read, watch the example and act.
            if (ahead < 12f && ahead > 1.5f && !_timeSlow.Active) StartTutorialSlowMotion(key);
        }

        void StartTutorialSlowMotion(string key)
        {
            _tutorialKey = key;
            _tutorialScaleBefore = Time.timeScale;
            Time.timeScale = _tutorialScaleBefore * TutorialTimeScale;
            TimetravelerLog.Info($"Tutorial {key}: slow motion.", this);
        }

        /// <summary>Back to normal time (always restores the time scale it changed: rule 6).</summary>
        void EndTutorialSlowMotion(bool learned)
        {
            if (_tutorialKey == null) return;
            if (learned) _save.MarkTutorialSeen(_tutorialKey);
            _tutorialKey = null;
            // While paused the pause restores the scale itself: give it the one to go back to.
            if (_paused) _scaleBeforePause = _tutorialScaleBefore;
            else Time.timeScale = _tutorialScaleBefore;
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
            EndTutorialSlowMotion(true);
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
            if (_parallax != null) _parallax.SetWorld(world);
        }

        void OnDied(string reason)
        {
            EndTutorialSlowMotion(false);
            _timeSlow.Restore();
            _deathTime = Time.unscaledTime;
            _fx.Burst(_traveler.transform.position, 18, RunnerShapes.HazardColor, new Vector2(0f, 6f), 8f, 0.45f, 0.8f);
            _follow.Shake(0.5f, 0.35f);
            TimetravelerLog.Info($"Died at {DistanceMeters} m: {reason} (world {RunnerWorldState.NameOf(_worlds.Current)}, speed {_traveler.RunSpeed:0.0}).", this);
            EndRunBookkeeping();
            if (_boss != null) _boss.OnTravelerDied();
        }

        void OnBossDefeated()
        {
            if (!_finished && !_traveler.IsDead) Finish();
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
            EndTutorialSlowMotion(true);
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
            _follow.screenAnchorX = RunnerCamera.RunAnchor;   // fixed framing: the view does not slide around with the speed
            if (_traveler.IsGrounded) _follow.groundY = Feet.y;

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
            EndTutorialSlowMotion(true);
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
                var era = _level != null ? _level.Era : 0;
                _background.sprite = TimetravelerArt.EraSprite(slot, era) ?? TimetravelerArt.EraSprite(TimetravelerArt.RunnerBackgroundPast, era);
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
            EndTutorialSlowMotion(false);
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
            EndTutorialSlowMotion(false);
            _timeSlow.Restore();
        }

        void OnDestroy()
        {
            Resume();
            EndTutorialSlowMotion(false);
            _timeSlow.Restore();
            if (_traveler != null)
            {
                _traveler.Died -= OnDied;
                _traveler.CoinTouched -= OnCoin;
                _traveler.Jumped -= OnJumped;
                _traveler.Landed -= OnLanded;
            }
            _worlds.Changed -= OnWorldChanged;
            if (_boss != null) _boss.Defeated -= OnBossDefeated;
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
            var top = Screen.height - safe.yMax;
            var width = safe.width - unit * 2f;
            var oldColor = GUI.color;
            DrawTopBar(unit, safe, top);

            _checkpointFlash -= Time.unscaledDeltaTime;
            if (_checkpointFlash > 0f && !_traveler.IsDead && !_finished)
                ShadowLabel(new Rect(0, Screen.height * 0.17f, Screen.width, unit * 2f), "CHECKPOINT!", _bigStyle,
                    new Color(0.5f, 0.95f, 1f, Mathf.Clamp01(_checkpointFlash)));

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

            var bossFight = _boss != null && _boss.InControl;
            // Hint for an obstacle met for the first time.
            string hintKey = null, hint = null;
            if (!_traveler.IsDead && !bossFight) _track.CurrentHint(_traveler.transform.position.x, out hintKey, out hint, out _);
            if (hint != null) DrawHint(unit, safe, hintKey, hint, _tutorialKey != null && _tutorialKey == hintKey);

            // Controls: on a phone only the gestures, and only at the start; with a keyboard a short key list.
            var touch = UnityEngine.InputSystem.Touchscreen.current != null || Application.isMobilePlatform;
            string legend = null;
            if (touch && _runTime < hintDuration)
                legend = "Tap: jump (again in the air = double)\nSwipe down: slide   Swipe up: slow time\nSwipe sideways: switch world";
            else if (!touch)
                legend = _runTime < hintDuration
                    ? "Space jump (twice = double)   S slide   E/Shift slow time   Q switch world"
                    : "Space jump   S slide   E/Shift slow   Q switch";
            if (legend != null && !bossFight && _settings.ShowControls)
                ShadowLabel(new Rect(safe.x + unit, Screen.height - safe.y - unit * 3.6f, width, unit * 3.4f), legend, _centerStyle, Color.white);

            if (_traveler.IsDead)
            {
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, unit * 2f),
                    _newRecord ? $"{DistanceMeters} m  NEW BEST!" : $"{DistanceMeters} m   +{_runCoins} coins", _bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.35f + unit * 2.2f, Screen.width, unit * 2f),
                    IsEndless ? "Tap to run again" : _checkpointReached ? "Tap: continue from checkpoint" : "Tap to try again", _bigStyle);
            }

            if (_finished)
            {
                DrawResults(unit);
                return;
            }

            DrawPause(unit);
        }

        GUIStyle _hintStyle, _gestureStyle;

        /// <summary>
        /// The hint for an obstacle: big text. The first time ever (slow motion on) also a looping example of the
        /// move with her sprites and the gesture in large letters, lit up in time with the example.
        /// </summary>
        void DrawHint(float unit, Rect safe, string key, string text, bool teaching)
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
                _gestureStyle = new GUIStyle(_hintStyle) { wordWrap = false };
            }
            _hintStyle.fontSize = Mathf.RoundToInt(unit * (teaching ? 1.15f : 1f));
            _gestureStyle.fontSize = Mathf.RoundToInt(unit * 1.6f);

            var width = safe.width - unit;
            var textH = unit * 4.2f;
            var demoH = teaching ? Mathf.Min(width * 0.55f, unit * 9f) : 0f;
            var gestureH = teaching ? unit * 2.6f : 0f;
            var box = new Rect(safe.x + unit * 0.5f, Screen.height * 0.2f, width, textH + demoH + gestureH + unit * 0.8f);
            Fill(box, new Color(0f, 0f, 0f, teaching ? 0.78f : 0.6f));
            ShadowLabel(new Rect(box.x + unit * 0.4f, box.y + unit * 0.3f, box.width - unit * 0.8f, textH), text, _hintStyle, Color.white);
            if (!teaching) return;

            var move = RunnerTutorial.MoveFor(key);
            var demo = new Rect(box.x + unit * 0.8f, box.y + unit * 0.3f + textH, box.width - unit * 1.6f, demoH);
            var acting = _tutorial.Draw(demo, move, Time.unscaledTime);
            var touch = UnityEngine.InputSystem.Touchscreen.current != null || Application.isMobilePlatform;
            var pulse = acting ? 1f : 0.55f;
            ShadowLabel(new Rect(box.x, demo.yMax + unit * 0.2f, box.width, gestureH), RunnerTutorial.GestureFor(move, touch),
                _gestureStyle, new Color(1f, 0.85f, 0.3f, pulse));
            ShadowLabel(new Rect(box.x, box.yMax, box.width, unit * 1.6f), "slow motion: do it now!", _centerStyle,
                new Color(0.6f, 0.9f, 1f, 0.9f));
        }

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color CoinColor = new Color(1f, 0.85f, 0.25f);
        GUIStyle _hudStyle, _hudSmallStyle;

        /// <summary>Text with a dark shadow: readable on any background, also outdoors on a phone.</summary>
        static void ShadowLabel(Rect r, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            var o = Mathf.Max(1f, style.fontSize / 14f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f * color.a);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>
        /// Phone HUD inside the safe area (notch-proof): progress bar (levels) or distance (endless), a coin chip,
        /// then the current world and the watch energy. Big, few words, shadowed.
        /// </summary>
        void DrawTopBar(float unit, Rect safe, float top)
        {
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _hudSmallStyle = new GUIStyle(_hudStyle) { alignment = TextAnchor.MiddleLeft };
            }
            _hudStyle.fontSize = Mathf.RoundToInt(unit * 0.85f);
            _hudSmallStyle.fontSize = Mathf.RoundToInt(unit * 0.7f);

            var left = safe.x + unit * 0.5f;
            var rowY = top + unit * 0.5f;
            var rowH = unit * 1.8f;
            var rightLimit = PauseButtonRectGui().x - unit * 0.4f;

            // Coins chip, left of the pause button.
            var coinRect = new Rect(rightLimit - unit * 4.4f, rowY, unit * 4.4f, rowH);
            Fill(coinRect, PanelColor);
            Fill(new Rect(coinRect.x + unit * 0.5f, rowY + rowH / 2f - unit * 0.4f, unit * 0.8f, unit * 0.8f), CoinColor);
            ShadowLabel(new Rect(coinRect.x + unit * 1.2f, rowY, coinRect.width - unit * 1.3f, rowH), _runCoins.ToString(),
                _hudStyle, Color.white);

            // Progress: a bar to the finish in a level, the distance and best in endless.
            var progRect = new Rect(left, rowY, coinRect.x - left - unit * 0.4f, rowH);
            Fill(progRect, PanelColor);
            if (IsEndless)
            {
                ShadowLabel(progRect, $"{DistanceMeters} m   best {_save.BestMeters}", _hudStyle, Color.white);
            }
            else
            {
                var t = Mathf.Clamp01(DistanceMeters / (float)_level.LengthMeters);
                var inner = new Rect(progRect.x + unit * 0.2f, progRect.y + unit * 0.2f, progRect.width - unit * 0.4f, rowH - unit * 0.4f);
                Fill(new Rect(inner.x, inner.y, inner.width * t, inner.height), new Color(0.35f, 0.75f, 0.35f, 0.85f));
                if (_track.CheckpointMeters > 0f)
                {
                    var cx = inner.x + inner.width * Mathf.Clamp01(_track.CheckpointMeters / _level.LengthMeters);
                    Fill(new Rect(cx - 1f, inner.y, 3f, inner.height), _checkpointReached ? CoinColor : Color.white);
                }
                ShadowLabel(progRect, $"{_level.Title}   {Mathf.Min(DistanceMeters, _level.LengthMeters)} / {_level.LengthMeters} m",
                    _hudStyle, Color.white);
            }

            // Second row: world chip (in its colour) and the watch energy.
            var row2 = rowY + rowH + unit * 0.35f;
            var worldRect = new Rect(left, row2, unit * 5f, unit * 1.4f);
            var wc = RunnerWorldState.ColorOf(_worlds.Current);
            Fill(worldRect, new Color(wc.r, wc.g, wc.b, 0.9f));
            ShadowLabel(worldRect, RunnerWorldState.NameOf(_worlds.Current), _hudStyle, Color.white);

            var bar = new Rect(worldRect.xMax + unit * 0.5f, row2 + unit * 0.35f, unit * 7f, unit * 0.7f);
            Fill(bar, PanelColor);
            var ready = _timeSlow.Energy >= _timeSlow.minEnergyToStart;
            var energyColor = _timeSlow.Active ? new Color(0.55f, 0.95f, 1f)
                : ready ? new Color(0.4f, 0.85f, 1f) : new Color(0.55f, 0.55f, 0.6f);
            Fill(new Rect(bar.x, bar.y, bar.width * _timeSlow.Energy, bar.height), energyColor);
            ShadowLabel(new Rect(bar.xMax + unit * 0.3f, row2, unit * 4f, unit * 1.4f), _timeSlow.Active ? "SLOW!" : "slow",
                _hudSmallStyle, energyColor);
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
