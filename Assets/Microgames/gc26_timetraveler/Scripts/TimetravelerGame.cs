using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Entry point of Time Traveler Runner inside the GC26 shell.
    /// The shared project rotates freely, so this game locks the screen to portrait
    /// while it runs and restores the previous orientation when it is unloaded (rule 6).
    /// Outside Play mode it shows a small preview (ground, traveler, obstacles) so the scene
    /// does not look empty in the editor; the preview is never saved into the scene.
    /// </summary>
    [ExecuteAlways]
    public class TimetravelerGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_timetraveler";

        Action<int> _exit;
        bool _started;

        ScreenOrientation _previousOrientation;
        bool _orientationLocked;

        GameObject _preview;

        public void Begin(Action<int> exit)
        {
            if (_started)
            {
                TimetravelerLog.Warn("Begin called twice, ignored.", this);
                return;
            }
            _started = true;
            _exit = exit;
            TimetravelerLog.Info($"Begin ({(MicrogameRunner.IsRunning ? "from the GC26 menu" : "scene opened on its own")}).", this);
            LockPortrait();
            StartFlow();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => TimetravelerLog.Info($"Exit to menu, score = {score} (no menu shell in this test run).", this));
        }

        void OnEnable()
        {
            if (!Application.isPlaying) ShowEditorPreview();
        }

        void OnDisable()
        {
            HideEditorPreview();
            RestoreOrientation();
        }

        /// <summary>Title screen -> world map -> levels (see TimetravelerFlow). Progress is loaded there.</summary>
        void StartFlow()
        {
            if (GetComponent<TimetravelerFlow>() != null) return;
            var cam = FindOwnCamera();
            if (cam == null)
            {
                TimetravelerLog.Error("No camera in the game scene, the runner cannot start.", this);
                return;
            }
            TimetravelerLog.Info($"Using camera '{cam.name}', screen {Screen.width}x{Screen.height}.", cam);
            gameObject.AddComponent<TimetravelerFlow>().Init(cam, ExitToMenu);
        }

        /// <summary>The camera in this game's own scene (the shell may have its own camera).</summary>
        Camera FindOwnCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null) return cam;
            }
            return null;
        }

        /// <summary>Call from the pause menu ("To menu") or the game-over screen.</summary>
        public void ExitToMenu(int score)
        {
            // Progress is already saved by TimetravelerSave whenever it changes.
            TimetravelerLog.Info($"ExitToMenu, score = {score}.", this);
            RestoreOrientation();
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }

        void LockPortrait()
        {
            if (_orientationLocked) return;
            _previousOrientation = Screen.orientation;
            Screen.orientation = ScreenOrientation.Portrait;
            _orientationLocked = true;
            TimetravelerLog.Info($"Screen locked to portrait (was {_previousOrientation}).", this);
        }

        void RestoreOrientation()
        {
            if (!_orientationLocked) return;
            Screen.orientation = _previousOrientation;
            _orientationLocked = false;
            TimetravelerLog.Info($"Screen orientation restored to {_previousOrientation}.", this);
        }

        // ---------- editor preview (not saved, not in Play mode) ----------

        void ShowEditorPreview()
        {
            HideEditorPreview();
            var cam = FindOwnCamera();
            var cx = cam != null ? cam.transform.position.x : 0f;
            var cy = cam != null ? cam.transform.position.y : 0f;

            _preview = new GameObject("TimetravelerEditorPreview") { hideFlags = HideFlags.HideAndDontSave };
            var shapes = new RunnerShapes(HideFlags.HideAndDontSave);
            var groundTop = cy - 10.5f;
            var left = cx - 12f;
            shapes.Box(_preview.transform, "PreviewGround", left, groundTop - 6f, 24f, 6f, RunnerShapes.GroundColor);
            var travelerFrame = TravelerAnimator.LoadPreviewFrame();
            if (travelerFrame != null)
            {
                var traveler = new GameObject("PreviewTraveler").AddComponent<SpriteRenderer>();
                traveler.transform.SetParent(_preview.transform, false);
                traveler.transform.position = new Vector3(cx - 4.2f, groundTop, 0f);   // pivot at the feet
                traveler.sprite = travelerFrame;
                traveler.sortingOrder = 2;
            }
            else
                shapes.Box(_preview.transform, "PreviewTraveler", cx - 5.2f, groundTop, 2f, 3f, RunnerWorldState.PastColor, sortingOrder: 2);
            shapes.Box(_preview.transform, "PreviewRock", cx + 1f, groundTop, 1.5f, 1.5f, RunnerShapes.HazardColor, sortingOrder: 1);
            shapes.Box(_preview.transform, "PreviewLog", cx + 5f, groundTop + 2f, 2.5f, 14f, RunnerShapes.HazardColor, sortingOrder: 1);
            foreach (var t in _preview.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        void HideEditorPreview()
        {
            if (_preview == null) return;
            if (Application.isPlaying) Destroy(_preview);
            else DestroyImmediate(_preview);
            _preview = null;
        }
    }
}
