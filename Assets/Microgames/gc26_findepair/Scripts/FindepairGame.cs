using System;
using GC26.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace GC26.Findepair
{
    /// <summary>
    /// Entry point and the only link to the GC26 shell (IMicrogame). Builds the game at Begin,
    /// switches between menu and board, and calls exit(score) once when the player leaves.
    /// Everything it creates is a child of this object, so it stays in this scene and is
    /// unloaded with it.
    /// </summary>
    public class FindepairGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_findepair";

        [SerializeField] FindepairArt art = new FindepairArt();

        Action<int> _exit;
        bool _started;
        int _sessionScore;

        public FindepairArt Art => art;
        public FindepairBoard Board { get; private set; }
        public FindepairUI UI { get; private set; }
        public FindepairAudio Audio { get; private set; }
        public FindepairSave Save { get; private set; }

        /// <summary>Stars of the best round finished in this session (0-3), reported to the shell.</summary>
        public int SessionScore => _sessionScore;

        public void Begin(Action<int> exit)
        {
            if (_started) return;
            _started = true;
            _exit = exit;

            Save = new FindepairSave();
            Save.Load();
            EnsureInputAndAudio();

            Audio = gameObject.AddComponent<FindepairAudio>();
            Audio.Init(Save);
            Board = gameObject.AddComponent<FindepairBoard>();
            UI = gameObject.AddComponent<FindepairUI>();
            UI.Build(this, art, Board, Save, Audio);
            Board.Setup(UI.Grid, UI.Sparkles, art);

            Board.CardFlipped += () => Audio.Play(FindepairSfx.Flip);
            Board.PairMatched += () => { Audio.Play(FindepairSfx.Match); Audio.Vibrate(40); };
            Board.PairMissed += () => Audio.Play(FindepairSfx.Miss);
            Board.Won += OnWon;

            UI.Show(FindepairScreen.Splash);
            Audio.SetMusic(true);
        }

        void Start()
        {
            // Scene opened on its own (no shell): start ourselves for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }

        public void StartGame()
        {
            UI.Show(FindepairScreen.Game);
            Board.NewGame();
        }

        public void ToMenu()
        {
            Board.Stop();
            UI.Show(FindepairScreen.Menu);
        }

        /// <summary>"Exit" button: save, stop, and hand control back to the GC26 menu (once).</summary>
        public void ExitToShell()
        {
            Board.Stop();
            Save.Save();
            var exit = _exit;
            _exit = null;
            if (MicrogameRunner.IsRunning) Audio.StopAll();
            exit?.Invoke(_sessionScore);
            // On its own there is no shell to return to: stay on the menu.
            if (!MicrogameRunner.IsRunning) UI.Show(FindepairScreen.Menu);
        }

        void OnWon(FindepairResult result)
        {
            _sessionScore = Mathf.Max(_sessionScore, result.Stars);
            bool record = Save.TrySetBest(result);
            Audio.Play(FindepairSfx.Win);
            Audio.VibrateWin();
            UI.ShowWin(result, record);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Save != null) Save.Save();
        }

        /// <summary>
        /// Inside the shell its camera, AudioListener and EventSystem are used. On its own the scene
        /// needs its own, created here as children so nothing leaks into other scenes.
        /// </summary>
        void EnsureInputAndAudio()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (!MicrogameRunner.IsRunning && FindAnyObjectByType<Camera>() == null)
            {
                var cam = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                cam.transform.SetParent(transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FindepairUIKit.Bg;
                cam.orthographic = true;
            }
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }
    }
}
